// <copyright file="AuctionCurrencyInventorySnapshotTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the inventory snapshot which backs the Auction House Sell preview.
/// </summary>
[TestFixture]
public class AuctionCurrencyInventorySnapshotTests
{
    /// <summary>
    /// Counts only clean main-inventory loose jewels and configured packs, preserving each denomination.
    /// </summary>
    [Test]
    public void JewelSnapshotCountsTenderableDenominations()
    {
        var loose = new ItemDefinition { Group = 12, Number = 15 };
        var pack = new ItemDefinition { Group = 12, Number = 30 };
        var mix = new JewelMix { SingleJewel = loose, MixedJewel = pack };
        var items = new[]
        {
            Item(loose, 0, 12),
            Item(loose, 0, 13),
            Item(pack, 0, 14),
            Item(pack, 1, 15),
            Item(pack, 2, 16),
            Item(pack, 3, 17), // unsupported pack level
            Item(loose, 0, 0), // equipment slot
            Item(loose, 0, 18, durability: 0),
        };

        var snapshot = AuctionCurrencyInventorySnapshot.CreateJewel(1, new[] { mix }, items);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsConfigured, Is.True);
            Assert.That(snapshot.LooseItemType, Is.EqualTo((12 * 512) + 15));
            Assert.That(snapshot.PackItemType, Is.EqualTo((12 * 512) + 30));
            Assert.That(snapshot.LooseCount, Is.EqualTo(2));
            Assert.That(snapshot.PackCounts, Is.EqualTo(new ushort[] { 1, 1, 1 }));
            Assert.That(snapshot.TotalUnits, Is.EqualTo(62));
        });
    }

    /// <summary>
    /// Counts each clean Fruit by its configured stat component.
    /// </summary>
    [Test]
    public void FruitSnapshotCountsTenderableComponents()
    {
        var fruit = new ItemDefinition { Group = 13, Number = 15 };
        var items = new[]
        {
            Item(fruit, (byte)FruitRules.GetItemLevel(FruitComponent.Strength), 12),
            Item(fruit, (byte)FruitRules.GetItemLevel(FruitComponent.Agility), 13),
            Item(fruit, (byte)FruitRules.GetItemLevel(FruitComponent.Agility), 14),
            Item(fruit, (byte)FruitRules.GetItemLevel(FruitComponent.Command), 15),
            Item(fruit, (byte)FruitRules.GetItemLevel(FruitComponent.Energy), 0),
        };

        var snapshot = AuctionFruitInventorySnapshot.Create(new[] { fruit }, items);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsConfigured, Is.True);
            Assert.That(snapshot.Spendable, Is.EqualTo(new FruitBasket(1, 2, 0, 0, 1)));
        });
    }

    private static Item Item(ItemDefinition definition, byte level, byte slot, double durability = 1)
        => new() { Definition = definition, Level = level, ItemSlot = slot, Durability = durability };
}
