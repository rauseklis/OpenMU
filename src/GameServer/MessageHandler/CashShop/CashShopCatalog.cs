// <copyright file="CashShopCatalog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.CashShop;

using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MUnique.OpenMU.Persistence.CashShop;

/// <summary>
/// The shop catalogue: the same script files the client shows (<c>IBSCategory.txt</c>, <c>IBSPackage.txt</c>,
/// <c>IBSProduct.txt</c>), combined with the list of OpenMU items the packages deliver.
/// </summary>
internal sealed class CashShopCatalog
{
    /// <summary>The sales zone of the script version the server announces to the client.</summary>
    internal const ushort ScriptZone = 512;

    /// <summary>The year of the announced script version.</summary>
    internal const ushort ScriptYear = 2012;

    /// <summary>The identifier of the announced script version (folder <c>512.2012.085</c> in the client's <c>Data/InGameShopScript</c>).</summary>
    internal const ushort ScriptYearIdentifier = 85;

    /// <summary>The sales zone of the banner version (folder <c>583.2012.001</c> in the client's <c>Data/InGameShopBanner</c>, which holds an empty banner list).</summary>
    internal const ushort BannerZone = 583;

    /// <summary>The year of the announced banner version.</summary>
    internal const ushort BannerYear = 2012;

    /// <summary>The identifier of the announced banner version.</summary>
    internal const ushort BannerYearIdentifier = 1;

    private const int ProductPropertyQuantity = 7;

    private const int ProductPropertyDuration = 10;

    private readonly Dictionary<int, Package> _packages = new();

    private CashShopCatalog()
    {
    }

    /// <summary>Gets the catalogue which is built from the embedded script files.</summary>
    internal static CashShopCatalog Default { get; } = LoadEmbedded();

    /// <summary>Gets the number of packages which can be bought.</summary>
    internal int PackageCount => this._packages.Count;

    /// <summary>Gets the item locations (group, number) which packages deliver.</summary>
    internal IEnumerable<(byte Group, short Number)> DeliveredItems
        => this._packages.Values.Select(p => (p.Group, p.Number)).Distinct();

    /// <summary>Parses a catalogue from the script texts.</summary>
    /// <param name="categories">The content of <c>IBSCategory.txt</c>.</param>
    /// <param name="packages">The content of <c>IBSPackage.txt</c>.</param>
    /// <param name="products">The content of <c>IBSProduct.txt</c>.</param>
    /// <param name="items">The content of <c>cashshop-items.csv</c>.</param>
    /// <returns>The catalogue.</returns>
    internal static CashShopCatalog Parse(string categories, string packages, string products, string items)
    {
        var itemMap = ParseItems(items);
        var rootCurrency = new Dictionary<int, CashShopCurrency>();
        var categoryRoot = new Dictionary<int, int>();
        foreach (var fields in Lines(categories))
        {
            var id = Int(fields[0]);
            var root = Int(fields[4]);
            categoryRoot[id] = root;
            if (fields[6] == "1")
            {
                rootCurrency[root] = root switch
                {
                    10 => CashShopCurrency.WCoinC,
                    11 => CashShopCurrency.WCoinP,
                    20 => CashShopCurrency.GoblinPoints,
                    _ => throw new InvalidDataException($"Unknown shop root category {root}."),
                };
            }
        }

        var options = new Dictionary<int, List<Option>>();
        foreach (var fields in Lines(products))
        {
            var productSeq = Int(fields[0]);
            if (!options.TryGetValue(productSeq, out var list))
            {
                list = new List<Option>();
                options[productSeq] = list;
            }

            var priceSeq = Int(fields[6]);
            var option = list.FirstOrDefault(o => o.PriceSeq == priceSeq);
            if (option is null)
            {
                option = new Option(priceSeq, Int(fields[5]));
                list.Add(option);
            }

            if (Int(fields[14]) == ProductPropertyDuration && fields[4] == "Sec.")
            {
                option.DurationSeconds = Int(fields[3]);
            }

            if (Int(fields[14]) == ProductPropertyQuantity)
            {
                option.Quantity = Math.Max(1, Int(fields[3]));
            }
        }

        var catalog = new CashShopCatalog();
        foreach (var fields in Lines(packages))
        {
            var category = Int(fields[0]);
            var name = Normalize(fields[3]);
            var productSeqs = fields[19].Split('|', StringSplitOptions.RemoveEmptyEntries).Select(Int).ToArray();
            if (!itemMap.TryGetValue(name, out var item) || !categoryRoot.TryGetValue(category, out var root)
                || !rootCurrency.TryGetValue(root, out var currency) || productSeqs.Length != 1
                || !options.TryGetValue(productSeqs[0], out var productOptions))
            {
                continue;
            }

            catalog._packages[Int(fields[2])] = new Package(
                Int(fields[2]),
                category,
                currency,
                productSeqs[0],
                Int(fields[5]),
                productOptions,
                item.Group,
                item.Number,
                item.Level);
        }

        return catalog;
    }

    /// <summary>Resolves what a buy request asks for. The client's values are only used as keys; price and item come from the script.</summary>
    /// <param name="packageSeq">The package sequence number.</param>
    /// <param name="categorySeq">The category (display) sequence number.</param>
    /// <param name="priceSeq">The price option, or 0 for the package's default option.</param>
    /// <param name="offer">The offer.</param>
    /// <returns>True when the package can be bought in this category.</returns>
    internal bool TryResolve(int packageSeq, int categorySeq, int priceSeq, out CashShopOffer offer)
    {
        offer = default!;
        if (!this._packages.TryGetValue(packageSeq, out var package) || package.CategorySeq != categorySeq)
        {
            return false;
        }

        var option = priceSeq == 0
            ? package.Options.FirstOrDefault(o => o.Price == package.Price) ?? package.Options[0]
            : package.Options.FirstOrDefault(o => o.PriceSeq == priceSeq);
        if (option is null)
        {
            return false;
        }

        var isPet = package.Group == 13 && package.Number is 0 or 1 or 2 or 3 or 4 or 5 or 37 or 64 or 65 or 67 or 80 or 106 or 123;
        if (isPet && option.DurationSeconds is not > 0)
        {
            return false; // Never charge for a new pet rental with an unknown duration.
        }

        offer = new CashShopOffer(
            package.Seq,
            package.ProductSeq,
            option.PriceSeq,
            package.Currency,
            option.Price > 0 ? option.Price : package.Price,
            package.Group,
            package.Number,
            package.Level,
            option.Quantity,
            isPet ? option.DurationSeconds : null);
        return true;
    }

    private static CashShopCatalog LoadEmbedded()
    {
        return Parse(
            ReadResource("IBSCategory.txt"),
            ReadResource("IBSPackage.txt"),
            ReadResource("IBSProduct.txt"),
            ReadResource("cashshop-items.csv"));
    }

    private static string ReadResource(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + fileName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream, Encoding.Latin1);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string[]> Lines(string text)
        => text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('@'));

    private static int Int(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static Dictionary<string, (byte Group, short Number, byte Level)> ParseItems(string csv)
    {
        var result = new Dictionary<string, (byte, short, byte)>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in csv.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')))
        {
            var parts = line.Split(',');
            result[parts[0].Trim()] = (byte.Parse(parts[1], CultureInfo.InvariantCulture), short.Parse(parts[2], CultureInfo.InvariantCulture), byte.Parse(parts[3], CultureInfo.InvariantCulture));
        }

        return result;
    }

    /// <summary>Reduces a script name to the name used in <c>cashshop-items.csv</c>: no bracket prefixes, no "_7 Day" suffix.</summary>
    private static string Normalize(string name)
    {
        var text = name.Trim();
        while (text.StartsWith('['))
        {
            var end = text.IndexOf(']');
            var rest = text[(end + 1)..].Trim();
            text = rest.Length > 0 ? rest : text[1..end];
        }

        var suffix = text.LastIndexOf('_');
        if (suffix > 0 && text.EndsWith(" Day", StringComparison.Ordinal) && int.TryParse(text[(suffix + 1)..^4], out _))
        {
            text = text[..suffix];
        }

        return text.Trim();
    }

    private sealed class Option(int priceSeq, int price)
    {
        public int PriceSeq { get; } = priceSeq;

        public int Price { get; } = price;

        public int Quantity { get; set; } = 1;

        public int? DurationSeconds { get; set; }
    }

    private sealed record Package(int Seq, int CategorySeq, CashShopCurrency Currency, int ProductSeq, int Price, List<Option> Options, byte Group, short Number, byte Level);
}

/// <summary>What a buy request resolved to.</summary>
/// <param name="PackageSeq">The package.</param>
/// <param name="ProductSeq">The product.</param>
/// <param name="PriceSeq">The chosen price option.</param>
/// <param name="Currency">The currency of the price.</param>
/// <param name="Price">The price.</param>
/// <param name="ItemGroup">The delivered item group.</param>
/// <param name="ItemNumber">The delivered item number.</param>
/// <param name="ItemLevel">The delivered item level.</param>
/// <param name="Quantity">The number of items.</param>
internal sealed record CashShopOffer(int PackageSeq, int ProductSeq, int PriceSeq, CashShopCurrency Currency, long Price, byte ItemGroup, short ItemNumber, byte ItemLevel, int Quantity, int? RentalDurationSeconds = null);
