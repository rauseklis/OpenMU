// <copyright file="JewelBankKinds.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// A kind of item which the Jewel Bank stores as a count: a jewel, or one type of stat fruit.
/// </summary>
/// <param name="Code">The kind as its wire and storage value. Codes are never reused.</param>
/// <param name="Group">The item group of the item definition.</param>
/// <param name="Number">The item number of the item definition.</param>
/// <param name="FruitLevel">The item level which selects the fruit type; <c>null</c> for jewels, which are stored at level 0.</param>
/// <param name="Name">The English name, used in logs and messages.</param>
public readonly record struct JewelBankKind(byte Code, byte Group, short Number, byte? FruitLevel, string Name)
{
    /// <summary>
    /// Gets a value indicating whether this kind is a stat fruit.
    /// </summary>
    public bool IsFruit => this.FruitLevel is not null;
}

/// <summary>
/// The items a deposit takes of one kind: whole packs of each level, and loose items.
/// </summary>
/// <param name="Packs">The number of packs to take, by pack level (0 = 10 units, 1 = 20, 2 = 30).</param>
/// <param name="Loose">The number of loose items to take.</param>
public readonly record struct DepositPlan(int[] Packs, int Loose)
{
    /// <summary>
    /// Gets the units the plan moves into the bank.
    /// </summary>
    public long Units
    {
        get
        {
            var units = (long)this.Loose;
            for (var level = 0; level < this.Packs.Length; level++)
            {
                units += (long)this.Packs[level] * JewelBankKinds.GetPackUnits(level);
            }

            return units;
        }
    }
}

/// <summary>
/// The catalog of the kinds which the Jewel Bank accepts. Anything which is not listed here can never enter the bank.
/// </summary>
public static class JewelBankKinds
{
    /// <summary>
    /// The item group of the stat fruits.
    /// </summary>
    public const byte FruitGroup = 13;

    /// <summary>
    /// The item number of the stat fruits; the fruit type is the item level.
    /// </summary>
    public const short FruitNumber = 15;

    /// <summary>
    /// The highest item level of a fruit.
    /// </summary>
    public const byte MaximumFruitLevel = 4;

    /// <summary>
    /// Gets all kinds, ordered by code.
    /// </summary>
    public static IReadOnlyList<JewelBankKind> All { get; } =
    [
        new(1, 12, 15, null, "Jewel of Chaos"),
        new(2, 14, 13, null, "Jewel of Bless"),
        new(3, 14, 14, null, "Jewel of Soul"),
        new(4, 14, 16, null, "Jewel of Life"),
        new(5, 14, 22, null, "Jewel of Creation"),
        new(6, 14, 31, null, "Jewel of Guardian"),
        new(7, 14, 42, null, "Jewel of Harmony"),
        new(8, 14, 41, null, "Gemstone"),
        new(9, 14, 43, null, "Lower Refining Stone"),
        new(10, 14, 44, null, "Higher Refining Stone"),
        new(11, FruitGroup, FruitNumber, 0, "Fruit of Energy"),
        new(12, FruitGroup, FruitNumber, 1, "Fruit of Vitality"),
        new(13, FruitGroup, FruitNumber, 2, "Fruit of Agility"),
        new(14, FruitGroup, FruitNumber, 3, "Fruit of Strength"),
        new(15, FruitGroup, FruitNumber, 4, "Fruit of Command"),
    ];

    /// <summary>
    /// Finds a kind by its code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The kind, or <c>null</c> when the code is unknown.</returns>
    public static JewelBankKind? FindByCode(byte code)
        => All.FirstOrDefault(k => k.Code == code) is { Code: not 0 } kind ? kind : null;

    /// <summary>
    /// Finds the kind of an item, if the bank accepts it.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="level">The item level.</param>
    /// <returns>The kind, or <c>null</c> when the bank does not accept this item.</returns>
    /// <remarks>Jewels are accepted at level 0 only (a higher level of a loose jewel is not a plain jewel). Fruits use the level as their type.</remarks>
    public static JewelBankKind? Find(byte group, short number, int level)
    {
        foreach (var kind in All)
        {
            if (kind.Group != group || kind.Number != number)
            {
                continue;
            }

            if (kind.FruitLevel is { } fruitLevel)
            {
                if (level == fruitLevel)
                {
                    return kind;
                }
            }
            else if (level == 0)
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the number of loose units a packed jewel of the given item level represents (10, 20 or 30).
    /// </summary>
    /// <param name="packLevel">The item level of the pack.</param>
    /// <returns>The units, or 0 when the level is not an approved pack level.</returns>
    public static int GetPackUnits(int packLevel) => packLevel switch
    {
        0 => 10,
        1 => 20,
        2 => 30,
        _ => 0,
    };

    /// <summary>
    /// Gets the pack levels, largest pack first, in the order withdrawals should use them.
    /// </summary>
    public static IReadOnlyList<byte> PackLevelsLargestFirst { get; } = [2, 1, 0];

    /// <summary>
    /// Plans which items a deposit of up to <paramref name="requested"/> units takes. Packs are taken first, the largest which still fits the
    /// remainder, then loose items fill the rest, so the result is as close to the request as the items allow and never above it.
    /// </summary>
    /// <param name="requested">The units the player asked for.</param>
    /// <param name="room">The units the bank can still take of the kind.</param>
    /// <param name="looseAvailable">The number of loose items the player carries.</param>
    /// <param name="packsAvailable">The number of packs the player carries, by pack level.</param>
    /// <returns>The items to take.</returns>
    public static DepositPlan PlanDeposit(long requested, long room, int looseAvailable, IReadOnlyList<int> packsAvailable)
    {
        var remaining = Math.Min(requested, room);
        var packs = new int[PackLevelsLargestFirst.Count];
        if (remaining <= 0)
        {
            return new DepositPlan(packs, 0);
        }

        foreach (var level in PackLevelsLargestFirst)
        {
            var size = GetPackUnits(level);
            var available = level < packsAvailable.Count ? packsAvailable[level] : 0;
            while (packs[level] < available && size <= remaining)
            {
                packs[level]++;
                remaining -= size;
            }
        }

        var loose = (int)Math.Min(remaining, looseAvailable);
        return new DepositPlan(packs, loose);
    }

    /// <summary>
    /// Splits an amount of loose units into packs (largest first) and loose items.
    /// </summary>
    /// <param name="units">The amount of units to hand out.</param>
    /// <param name="canPack">Whether the kind has a packed form.</param>
    /// <returns>The pack levels to create and the number of loose items to create.</returns>
    public static (IReadOnlyList<byte> PackLevels, int Loose) Split(long units, bool canPack)
    {
        var packs = new List<byte>();
        var remaining = units;
        if (canPack)
        {
            foreach (var level in PackLevelsLargestFirst)
            {
                var size = GetPackUnits(level);
                while (remaining >= size)
                {
                    packs.Add(level);
                    remaining -= size;
                }
            }
        }

        return (packs, (int)remaining);
    }
}
