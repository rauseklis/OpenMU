// <copyright file="JewelBankKinds.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// A kind of item which the Jewel Bank stores as a count: a jewel, a stat fruit of one type, or a bundle (pack) of one jewel of one size.
/// Every kind is exactly one item definition at one item level, so a deposit removes that many items and a withdrawal creates that many.
/// </summary>
/// <param name="Code">The kind as its wire and storage value. Codes are never reused.</param>
/// <param name="Group">The item group of the item definition.</param>
/// <param name="Number">The item number of the item definition.</param>
/// <param name="ItemLevel">The item level the item has: 0 for jewels, the fruit type for fruits, the pack size level (0, 1, 2) for bundles.</param>
/// <param name="Name">The English name, used in logs and messages.</param>
/// <param name="PackUnits">The number of jewels a bundle holds (10, 20 or 30); 0 for everything which is not a bundle.</param>
public readonly record struct JewelBankKind(byte Code, byte Group, short Number, byte ItemLevel, string Name, int PackUnits = 0)
{
    /// <summary>
    /// Gets a value indicating whether this kind is a bundle of jewels.
    /// </summary>
    public bool IsPack => this.PackUnits > 0;
}

/// <summary>
/// The catalog of the kinds which the Jewel Bank accepts. Anything which is not listed here can never enter the bank.
/// </summary>
/// <remarks>
/// Codes 1 to 10 are the jewels and refining stones, 11 to 15 the fruits, and 16 to 45 the bundles: ten jewels in the order of
/// codes 1 to 10, first as bundles of 10 (codes 16 to 25), then of 20 (26 to 35), then of 30 (36 to 45).
/// </remarks>
public static class JewelBankKinds
{
    /// <summary>
    /// The number of kinds.
    /// </summary>
    public const int Count = 45;

    /// <summary>
    /// The number of jewels (and refining stones) which have bundles.
    /// </summary>
    public const int JewelCount = 10;

    /// <summary>
    /// The code of the first fruit kind (Fruit of Energy). The five fruits have the codes 11 to 15.
    /// </summary>
    public const byte FirstFruitCode = 11;

    /// <summary>
    /// The code of the last fruit kind (Fruit of Command).
    /// </summary>
    public const byte LastFruitCode = 15;

    /// <summary>
    /// The item group of the bundles.
    /// </summary>
    public const byte PackGroup = 12;

    private static readonly (byte Group, short Number, string Name, short PackNumber, string PackName)[] Jewels =
    [
        (12, 15, "Jewel of Chaos", 141, "Chaos Bundle"),
        (14, 13, "Jewel of Bless", 30, "Bless Bundle"),
        (14, 14, "Jewel of Soul", 31, "Soul Bundle"),
        (14, 16, "Jewel of Life", 136, "Life Bundle"),
        (14, 22, "Jewel of Creation", 137, "Creation Bundle"),
        (14, 31, "Jewel of Guardian", 138, "Guardian Bundle"),
        (14, 42, "Jewel of Harmony", 140, "Harmony Bundle"),
        (14, 41, "Gemstone", 139, "Gemstone Bundle"),
        (14, 43, "Lower Refining Stone", 142, "Lower Refining Bundle"),
        (14, 44, "Higher Refining Stone", 143, "Higher Refining Bundle"),
    ];

    private static readonly string[] FruitNames = ["Fruit of Energy", "Fruit of Vitality", "Fruit of Agility", "Fruit of Strength", "Fruit of Command"];

    /// <summary>
    /// Gets all kinds, ordered by code.
    /// </summary>
    public static IReadOnlyList<JewelBankKind> All { get; } = Create();

    /// <summary>
    /// Gets the number of jewels a bundle of the given item level holds (10, 20 or 30).
    /// </summary>
    /// <param name="packLevel">The item level of the bundle.</param>
    /// <returns>The units, or 0 when the level is not a bundle size.</returns>
    public static int GetPackUnits(int packLevel) => packLevel switch
    {
        0 => 10,
        1 => 20,
        2 => 30,
        _ => 0,
    };

    /// <summary>
    /// Finds a kind by its code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The kind, or <c>null</c> when the code is unknown.</returns>
    public static JewelBankKind? FindByCode(byte code)
        => code is >= 1 and <= Count ? All[code - 1] : null;

    /// <summary>
    /// Finds the kind of an item, if the bank accepts it.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="level">The item level.</param>
    /// <returns>The kind, or <c>null</c> when the bank does not accept this item.</returns>
    /// <remarks>An upgraded loose jewel is not a plain jewel and not accepted; a fruit's level is its type; a bundle's level is its size.</remarks>
    public static JewelBankKind? Find(byte group, short number, int level)
    {
        foreach (var kind in All)
        {
            if (kind.Group == group && kind.Number == number && kind.ItemLevel == level)
            {
                return kind;
            }
        }

        return null;
    }

    private static List<JewelBankKind> Create()
    {
        var kinds = new List<JewelBankKind>(Count);
        byte code = 1;
        foreach (var jewel in Jewels)
        {
            kinds.Add(new JewelBankKind(code++, jewel.Group, jewel.Number, 0, jewel.Name));
        }

        for (byte level = 0; level < FruitNames.Length; level++)
        {
            kinds.Add(new JewelBankKind(code++, 13, 15, level, FruitNames[level]));
        }

        for (byte packLevel = 0; packLevel < 3; packLevel++)
        {
            foreach (var jewel in Jewels)
            {
                kinds.Add(new JewelBankKind(code++, PackGroup, jewel.PackNumber, packLevel, jewel.PackName, GetPackUnits(packLevel)));
            }
        }

        return kinds;
    }
}
