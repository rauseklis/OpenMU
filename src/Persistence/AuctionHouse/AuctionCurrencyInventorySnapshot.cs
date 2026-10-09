// <copyright file="AuctionCurrencyInventorySnapshot.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Describes the clean, main-inventory jewels which the Auction House tender rules can spend.
/// </summary>
public sealed record AuctionCurrencyInventorySnapshot(
    bool IsConfigured,
    ushort LooseItemType,
    ushort PackItemType,
    ushort LooseCount,
    ushort[] PackCounts,
    uint TotalUnits)
{
    /// <summary>
    /// Builds a snapshot for one jewel currency using the same item constraints as transactional tendering.
    /// </summary>
    /// <param name="currency">The currency wire value.</param>
    /// <param name="jewelMixes">The configured jewel mixes.</param>
    /// <param name="items">The character inventory items.</param>
    /// <returns>The snapshot, or an unconfigured snapshot when the currency has no complete jewel mix.</returns>
    public static AuctionCurrencyInventorySnapshot CreateJewel(byte currency, IEnumerable<JewelMix> jewelMixes, IEnumerable<Item> items)
    {
        if (AuctionJewelIdentities.GetLooseJewel(currency) is not { } looseIdentity)
        {
            return Empty;
        }

        var mix = jewelMixes.FirstOrDefault(candidate =>
            HasIdentity(candidate.SingleJewel, looseIdentity.Group, looseIdentity.Number)
            && candidate.MixedJewel is not null);
        if (mix?.SingleJewel is not { } loose || mix.MixedJewel is not { } pack)
        {
            return Empty;
        }

        var looseCount = 0;
        var packCounts = new int[3];
        foreach (var item in items.Where(IsTenderable))
        {
            if (HasIdentity(item.Definition, loose.Group, loose.Number) && item.Level == 0)
            {
                looseCount++;
            }
            else if (HasIdentity(item.Definition, pack.Group, pack.Number) && item.Level < packCounts.Length)
            {
                packCounts[item.Level]++;
            }
        }

        var total = (long)looseCount;
        for (var index = 0; index < packCounts.Length; index++)
        {
            total += (long)packCounts[index] * JewelPackRules.GetPackUnits(index);
        }

        return new AuctionCurrencyInventorySnapshot(
            true,
            GetItemType(loose),
            GetItemType(pack),
            ClampCount(looseCount),
            packCounts.Select(ClampCount).ToArray(),
            (uint)Math.Min(total, uint.MaxValue));
    }

    private static AuctionCurrencyInventorySnapshot Empty { get; } = new(false, 0, 0, 0, new ushort[3], 0);

    private static bool IsTenderable(Item item)
        => item.ItemSlot >= InventoryConstants.EquippableSlotsCount
           && item.Durability > 0
           && item.SocketCount == 0
           && (item.ItemOptions is null || item.ItemOptions.Count == 0);

    private static bool HasIdentity(ItemDefinition? definition, byte group, short number)
        => definition?.Group == group && definition.Number == number;

    private static ushort GetItemType(ItemDefinition definition)
        => (ushort)((definition.Group * 512) + definition.Number);

    private static ushort ClampCount(int count) => (ushort)Math.Min(count, ushort.MaxValue);
}

/// <summary>
/// Describes the clean Fruits which the Auction House tender rules can spend.
/// </summary>
public readonly record struct AuctionFruitInventorySnapshot(bool IsConfigured, ushort FruitItemType, FruitBasket Spendable)
{
    /// <summary>
    /// Builds a Fruits snapshot using the same item constraints as transactional tendering.
    /// </summary>
    /// <param name="definitions">The configured item definitions.</param>
    /// <param name="items">The character inventory items.</param>
    /// <returns>The configured state and spendable quantities.</returns>
    public static AuctionFruitInventorySnapshot Create(IEnumerable<ItemDefinition> definitions, IEnumerable<Item> items)
    {
        var fruit = definitions.FirstOrDefault(definition => definition.Group == 13 && definition.Number == 15);
        if (fruit is null)
        {
            return new AuctionFruitInventorySnapshot(false, default, default);
        }

        var counts = new long[5];
        foreach (var item in items.Where(item =>
                     item.Definition?.Group == fruit.Group
                     && item.Definition.Number == fruit.Number
                     && item.ItemSlot >= InventoryConstants.EquippableSlotsCount
                     && item.Durability > 0
                     && item.SocketCount == 0
                     && (item.ItemOptions is null || item.ItemOptions.Count == 0)))
        {
            foreach (var component in Enum.GetValues<FruitComponent>())
            {
                if (item.Level == FruitRules.GetItemLevel(component))
                {
                    counts[(int)component]++;
                    break;
                }
            }
        }

        return new AuctionFruitInventorySnapshot(
            true,
            GetItemType(fruit),
            new FruitBasket(
                counts[(int)FruitComponent.Strength],
                counts[(int)FruitComponent.Agility],
                counts[(int)FruitComponent.Vitality],
                counts[(int)FruitComponent.Energy],
                counts[(int)FruitComponent.Command]));
    }

    private static ushort GetItemType(ItemDefinition definition)
        => (ushort)((definition.Group * 512) + definition.Number);
}
