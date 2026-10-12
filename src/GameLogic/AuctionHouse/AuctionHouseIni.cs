// <copyright file="AuctionHouseIni.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Globalization;
using System.Text;

/// <summary>
/// The outcome of reading an <c>AuctionHouse.ini</c>.
/// </summary>
/// <param name="AppliedCount">How many settings were read and applied.</param>
/// <param name="Errors">One message per line which was skipped, each with its line number.</param>
public sealed record AuctionHouseIniResult(int AppliedCount, IReadOnlyList<string> Errors);

/// <summary>
/// Reads and writes the hand-editable <c>AuctionHouse.ini</c>. The file lists every Auction House setting with a short
/// description, so the owner can change a fee, a limit or the on/off switch with a text editor instead of the admin
/// panel. A line which cannot be applied is reported and skipped; it never stops the rest of the file.
/// </summary>
public static class AuctionHouseIni
{
    private static readonly Setting[] Settings = CreateSettings();

    private static readonly Dictionary<string, Setting> SettingsByName =
        Settings.ToDictionary(setting => setting.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the name of every setting the file understands, in file order.
    /// </summary>
    public static IReadOnlyList<string> SettingNames { get; } = Settings.Select(setting => setting.Name).ToList();

    /// <summary>
    /// Applies the settings in the text over a configuration. Section headers are ignored (a setting is found by its
    /// name alone), comments start with <c>;</c> or <c>#</c>, and names are not case sensitive.
    /// </summary>
    /// <param name="configuration">The configuration to change.</param>
    /// <param name="text">The text of the file.</param>
    /// <returns>How many settings were applied and which lines were skipped.</returns>
    public static AuctionHouseIniResult Apply(AuctionHouseConfiguration configuration, string text)
    {
        var errors = new List<string>();
        var applied = new List<string>();
        var before = PriceRanges.Of(configuration);
        var lineNumber = 0;
        foreach (var rawLine in text.Split('\n'))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] is ';' or '#' or '[')
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                errors.Add($"line {lineNumber}: expected 'Name=Value', found '{line}'.");
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (!SettingsByName.TryGetValue(name, out var setting))
            {
                errors.Add($"line {lineNumber}: unknown setting '{name}'.");
                continue;
            }

            var problem = setting.TryApply(configuration, value);
            if (problem is null)
            {
                applied.Add(setting.Name);
            }
            else
            {
                errors.Add($"line {lineNumber}: {setting.Name}: {problem}");
            }
        }

        RejectInvertedRanges(configuration, before, applied, errors);
        return new AuctionHouseIniResult(applied.Count, errors);
    }

    /// <summary>
    /// Creates the text of the file for a configuration: every setting with its description and current value.
    /// </summary>
    /// <param name="configuration">The configuration to write.</param>
    /// <returns>The text of the file.</returns>
    public static string CreateText(AuctionHouseConfiguration configuration)
    {
        var builder = new StringBuilder();
        builder.AppendLine("; Auction House settings.");
        builder.AppendLine("; Edit a value and save: the running server picks the change up within a few seconds.");
        builder.AppendLine("; Lines starting with ; or # are comments. A line the server cannot use is reported in its log and skipped.");
        builder.AppendLine("; Values in this file take precedence over the settings in the admin panel.");
        string? section = null;
        foreach (var setting in Settings)
        {
            if (setting.Section != section)
            {
                section = setting.Section;
                builder.AppendLine();
                builder.AppendLine($"[{section}]");
            }

            builder.AppendLine($"; {setting.Description}");
            builder.AppendLine($"{setting.Name}={setting.Format(configuration)}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// The six price range settings as they were before a file was applied, so an inverted pair can be put back.
    /// </summary>
    private readonly record struct PriceRanges(long ZenMinimum, long ZenMaximum, long JewelMinimum, long JewelMaximum, long FruitMinimum, long FruitMaximum)
    {
        public static PriceRanges Of(AuctionHouseConfiguration c)
            => new(c.ZenMinimumPrice, c.ZenMaximumPrice, c.JewelMinimumPrice, c.JewelMaximumPrice, c.FruitMinimumPrice, c.FruitMaximumPrice);
    }

    private static void RejectInvertedRanges(AuctionHouseConfiguration configuration, PriceRanges before, List<string> applied, List<string> errors)
    {
        if (configuration.ZenMinimumPrice > configuration.ZenMaximumPrice)
        {
            Report("Zen", configuration.ZenMinimumPrice, configuration.ZenMaximumPrice, "ZenMinimum", "ZenMaximum");
            configuration.ZenMinimumPrice = before.ZenMinimum;
            configuration.ZenMaximumPrice = before.ZenMaximum;
        }

        if (configuration.JewelMinimumPrice > configuration.JewelMaximumPrice)
        {
            Report("Jewel", configuration.JewelMinimumPrice, configuration.JewelMaximumPrice, "JewelMinimum", "JewelMaximum");
            configuration.JewelMinimumPrice = before.JewelMinimum;
            configuration.JewelMaximumPrice = before.JewelMaximum;
        }

        if (configuration.FruitMinimumPrice > configuration.FruitMaximumPrice)
        {
            Report("Fruit", configuration.FruitMinimumPrice, configuration.FruitMaximumPrice, "FruitMinimum", "FruitMaximum");
            configuration.FruitMinimumPrice = before.FruitMinimum;
            configuration.FruitMaximumPrice = before.FruitMaximum;
        }

        void Report(string label, long minimum, long maximum, string minimumName, string maximumName)
        {
            applied.RemoveAll(name => name == minimumName || name == maximumName);
            errors.Add($"{label} price range: the minimum ({minimum}) is above the maximum ({maximum}); both settings were ignored.");
        }
    }

    private static Setting[] CreateSettings()
    {
        const string general = "General";
        const string fees = "Fees";
        const string prices = "Prices";
        const string worker = "Worker";
        return new[]
        {
            Bool(general, "Enabled", "Turn on new listings, bids, buyouts, cancels and collections. Browsing and recovery always work.", c => c.Enabled, (c, v) => c.Enabled = v),
            Whole(general, "MinimumCharacterLevel", "Character level needed to use the Auction House (1-400).", 1, 400, c => c.MinimumCharacterLevel, (c, v) => c.MinimumCharacterLevel = v),
            Whole(general, "MinimumAccountAgeHours", "Hours an account must exist before it can use the Auction House (0-8760).", 0, 8760, c => c.MinimumAccountAgeHours, (c, v) => c.MinimumAccountAgeHours = v),
            Whole(general, "PageSize", "Listings per Browse page (1-100).", 1, 100, c => c.PageSize, (c, v) => c.PageSize = v),
            Whole(general, "MaximumActiveListingsPerAccount", "Active listings one account may have (1-1000).", 1, 1000, c => c.MaximumActiveListingsPerAccount, (c, v) => c.MaximumActiveListingsPerAccount = v),
            Whole(general, "MaximumActiveListingsPerCharacter", "Active listings one character may have (1-1000).", 1, 1000, c => c.MaximumActiveListingsPerCharacter, (c, v) => c.MaximumActiveListingsPerCharacter = v),
            Currency(fees, "SaleFeeCurrency", "What the flat sale fee is paid in: Zen, or WCoin (reserved, not charged yet).", c => c.SaleFeeCurrency, (c, v) => c.SaleFeeCurrency = v),
            Long(fees, "SaleFeeAmount", "Flat fee per completed sale, paid by the seller, held at listing and refunded if unsold. 0 = no fee. Takes effect when mixed prices go live.", 0, long.MaxValue, c => c.SaleFeeAmount, (c, v) => c.SaleFeeAmount = v),
            Whole(fees, "ListingFeeBasisPoints", "Legacy listing fee in basis points of the starting price (100 = 1%). Replaced by the flat sale fee.", 0, 10_000, c => c.ListingFeeBasisPoints, (c, v) => c.ListingFeeBasisPoints = v),
            Whole(fees, "SuccessFeeBasisPoints", "Legacy success fee in basis points of the final price (500 = 5%). Replaced by the flat sale fee.", 0, 10_000, c => c.SuccessFeeBasisPoints, (c, v) => c.SuccessFeeBasisPoints = v),
            Whole(prices, "MaximumPriceComponents", "Most kinds of currency one price may mix (1-13). Takes effect when mixed prices go live.", 1, 13, c => c.MaximumPriceComponents, (c, v) => c.MaximumPriceComponents = v),
            Long(prices, "ZenMinimum", "Smallest Zen amount in a price.", 1, 2_000_000_000, c => c.ZenMinimumPrice, (c, v) => c.ZenMinimumPrice = v),
            Long(prices, "ZenMaximum", "Largest Zen amount in a price.", 1, 2_000_000_000, c => c.ZenMaximumPrice, (c, v) => c.ZenMaximumPrice = v),
            Long(prices, "JewelMinimum", "Smallest amount of one jewel kind in a price.", 1, 1_000_000, c => c.JewelMinimumPrice, (c, v) => c.JewelMinimumPrice = v),
            Long(prices, "JewelMaximum", "Largest amount of one jewel kind in a price.", 1, 1_000_000, c => c.JewelMaximumPrice, (c, v) => c.JewelMaximumPrice = v),
            Long(prices, "FruitMinimum", "Smallest amount of one stat fruit kind in a price.", 1, 1_000_000, c => c.FruitMinimumPrice, (c, v) => c.FruitMinimumPrice = v),
            Long(prices, "FruitMaximum", "Largest amount of one stat fruit kind in a price.", 1, 1_000_000, c => c.FruitMaximumPrice, (c, v) => c.FruitMaximumPrice = v),
            Whole(worker, "ExpiryIntervalSeconds", "Seconds between two checks for listings that have ended (1-3600).", 1, 3600, c => c.ExpiryIntervalSeconds, (c, v) => c.ExpiryIntervalSeconds = v),
            Whole(worker, "ExpiryBatchSize", "Ended listings settled per check (1-1000).", 1, 1000, c => c.ExpiryBatchSize, (c, v) => c.ExpiryBatchSize = v),
            Bool(worker, "NotificationsEnabled", "Show outbid/sold/bought messages to online players.", c => c.NotificationsEnabled, (c, v) => c.NotificationsEnabled = v),
            Whole(worker, "NotificationBatchSize", "Notifications delivered per check (1-1000).", 1, 1000, c => c.NotificationBatchSize, (c, v) => c.NotificationBatchSize = v),
            Whole(worker, "OperationRetentionDays", "Days a request record is kept before it may be purged (1-365).", 1, 365, c => c.OperationRetentionDays, (c, v) => c.OperationRetentionDays = v),
            Whole(worker, "PurgeBatchSize", "Request records purged per check (1-10000).", 1, 10_000, c => c.PurgeBatchSize, (c, v) => c.PurgeBatchSize = v),
        };
    }

    private static Setting Bool(string section, string name, string description, Func<AuctionHouseConfiguration, bool> get, Action<AuctionHouseConfiguration, bool> set)
    {
        return new Setting(
            section,
            name,
            description,
            c => get(c) ? "true" : "false",
            (c, text) =>
            {
                switch (text.ToLowerInvariant())
                {
                    case "true" or "1" or "yes" or "on":
                        set(c, true);
                        return null;
                    case "false" or "0" or "no" or "off":
                        set(c, false);
                        return null;
                    default:
                        return "expected true or false.";
                }
            });
    }

    private static Setting Whole(string section, string name, string description, int minimum, int maximum, Func<AuctionHouseConfiguration, int> get, Action<AuctionHouseConfiguration, int> set)
    {
        return new Setting(
            section,
            name,
            description,
            c => get(c).ToString(CultureInfo.InvariantCulture),
            (c, text) =>
            {
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    return "expected a whole number.";
                }

                if (value < minimum || value > maximum)
                {
                    return $"must be between {minimum} and {maximum}.";
                }

                set(c, value);
                return null;
            });
    }

    private static Setting Long(string section, string name, string description, long minimum, long maximum, Func<AuctionHouseConfiguration, long> get, Action<AuctionHouseConfiguration, long> set)
    {
        return new Setting(
            section,
            name,
            description,
            c => get(c).ToString(CultureInfo.InvariantCulture),
            (c, text) =>
            {
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    return "expected a whole number.";
                }

                if (value < minimum || value > maximum)
                {
                    return $"must be between {minimum} and {maximum}.";
                }

                set(c, value);
                return null;
            });
    }

    private static Setting Currency(string section, string name, string description, Func<AuctionHouseConfiguration, SaleFeeCurrency> get, Action<AuctionHouseConfiguration, SaleFeeCurrency> set)
    {
        return new Setting(
            section,
            name,
            description,
            c => get(c).ToString(),
            (c, text) =>
            {
                if (!Enum.TryParse<SaleFeeCurrency>(text, true, out var value) || !Enum.IsDefined(value))
                {
                    return "expected Zen or WCoin.";
                }

                set(c, value);
                return null;
            });
    }

    private sealed record Setting(
        string Section,
        string Name,
        string Description,
        Func<AuctionHouseConfiguration, string> Format,
        Func<AuctionHouseConfiguration, string, string?> TryApply);
}
