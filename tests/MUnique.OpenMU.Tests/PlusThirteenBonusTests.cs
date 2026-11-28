// <copyright file="PlusThirteenBonusTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Tests the bonuses of +13 items.
/// </summary>
[TestFixture]
public class PlusThirteenBonusTests
{
    /// <summary>
    /// A complete set of the same kind with all pieces at +13 gets the set bonus.
    /// </summary>
    [Test]
    public void CompleteSetGivesBonus()
    {
        Assert.That(PlusThirteenBonus.IsCompleteSet(CreateSet(13, 13, 13, 13, 13)), Is.True);
    }

    /// <summary>
    /// Pieces above +13 still count.
    /// </summary>
    [Test]
    public void HigherLevelCounts()
    {
        Assert.That(PlusThirteenBonus.IsCompleteSet(CreateSet(13, 14, 15, 13, 13)), Is.True);
    }

    /// <summary>
    /// A single piece below +13 prevents the bonus.
    /// </summary>
    [Test]
    public void OnePieceTooLowGivesNoBonus()
    {
        Assert.That(PlusThirteenBonus.IsCompleteSet(CreateSet(13, 13, 12, 13, 13)), Is.False);
    }

    /// <summary>
    /// Four pieces are not a complete set.
    /// </summary>
    [Test]
    public void MissingPieceGivesNoBonus()
    {
        Assert.That(PlusThirteenBonus.IsCompleteSet(CreateSet(13, 13, 13, 13, 13).Take(4)), Is.False);
    }

    /// <summary>
    /// Pieces of different sets do not give the bonus.
    /// </summary>
    [Test]
    public void MixedSetsGiveNoBonus()
    {
        var items = CreateSet(13, 13, 13, 13, 13).ToList();
        items[2] = CreateItem(InventoryConstants.PantsSlot, 9, 99, 13);
        Assert.That(PlusThirteenBonus.IsCompleteSet(items), Is.False);
    }

    /// <summary>
    /// A broken piece does not count.
    /// </summary>
    [Test]
    public void BrokenPieceGivesNoBonus()
    {
        var items = CreateSet(13, 13, 13, 13, 13).ToList();
        items[1].Durability = 0;
        Assert.That(PlusThirteenBonus.IsCompleteSet(items), Is.False);
    }

    /// <summary>
    /// The set gives exactly +100 vitality and 5% defense ignore chance, and no weapon strike.
    /// </summary>
    [Test]
    public void SetPowerUpsAreCorrect()
    {
        var holder = GetAttributeSystem();
        var powerUps = PlusThirteenBonus.GetPowerUps(CreateSet(13, 13, 13, 13, 13), holder).ToList();
        Assert.That(holder[Stats.TotalVitality], Is.EqualTo(100));
        Assert.That(holder[Stats.DefenseIgnoreChance], Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(holder[Stats.PlusThirteenStrikeChance], Is.EqualTo(0));
        Assert.That(powerUps, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// The +100 vitality is real, not cosmetic: it goes through the same relationships as a character's own stats.
    /// For an elf (2 maximum health per total vitality, as in the class definition) a full +13 set with a base
    /// vitality of 2020 raises the total vitality from 2020 to 2120 and the maximum health by exactly 200.
    /// Taking one piece off (or having it below +13) removes the whole bonus again. Every change of an attribute
    /// is sent to the client by the player (OnAttributeValueChanged), so the client gets the new maximum health.
    /// </summary>
    [Test]
    public void SetVitalityRaisesMaximumHealthAndGoesAwayWithAPiece()
    {
        var holder = new AttributeSystem(
            new IAttribute[] { new StatAttribute(Stats.BaseVitality, 2020) },
            Enumerable.Empty<IAttribute>(),
            new[]
            {
                new AttributeRelationship(Stats.TotalVitality, 1, Stats.BaseVitality),
                new AttributeRelationship(Stats.MaximumHealth, 2, Stats.TotalVitality),
            });
        var healthWithoutSet = holder[Stats.MaximumHealth];
        Assert.That(holder[Stats.TotalVitality], Is.EqualTo(2020));

        var withSet = PlusThirteenBonus.GetPowerUps(CreateSet(13, 13, 13, 13, 13), holder).ToList();
        Assert.That(holder[Stats.TotalVitality], Is.EqualTo(2120));
        Assert.That(holder[Stats.MaximumHealth], Is.EqualTo(healthWithoutSet + 200));

        // Equipment changed: the old power-ups are disposed and new ones are created for what is worn now.
        withSet.ForEach(p => p.Dispose());
        var brokenSet = PlusThirteenBonus.GetPowerUps(CreateSet(13, 13, 12, 13, 13), holder).ToList();
        Assert.That(brokenSet, Is.Empty);
        Assert.That(holder[Stats.TotalVitality], Is.EqualTo(2020));
        Assert.That(holder[Stats.MaximumHealth], Is.EqualTo(healthWithoutSet));
    }

    /// <summary>
    /// A +13 weapon gives a 6% strike chance; a +12 weapon or a shield gives nothing.
    /// </summary>
    [Test]
    public void WeaponGivesStrikeChanceOnlyAtThirteen()
    {
        var holder = GetAttributeSystem();
        PlusThirteenBonus.GetPowerUps(new[] { CreateItem(InventoryConstants.LeftHandSlot, 0, 5, 13) }, holder).ToList();
        Assert.That(holder[Stats.PlusThirteenStrikeChance], Is.EqualTo(0.06f).Within(0.0001f));

        Assert.That(PlusThirteenBonus.HasWeapon(new[] { CreateItem(InventoryConstants.LeftHandSlot, 0, 5, 12) }), Is.False);
        Assert.That(PlusThirteenBonus.HasWeapon(new[] { CreateItem(InventoryConstants.RightHandSlot, 6, 5, 13) }), Is.False);
    }

    /// <summary>
    /// The strike chance stays below the damage factor rule: 50% on top of excellent damage.
    /// </summary>
    [Test]
    public void ConstantsMatchTheDesign()
    {
        Assert.That(PlusThirteenBonus.WeaponStrikeChance, Is.EqualTo(0.06f));
        Assert.That(PlusThirteenBonus.WeaponStrikeDamageFactor, Is.EqualTo(1.5));
        Assert.That(PlusThirteenBonus.SetVitality, Is.EqualTo(100));
        Assert.That(PlusThirteenBonus.SetDefenseIgnoreChance, Is.EqualTo(0.05f));
    }

    private static IEnumerable<Item> CreateSet(params byte[] levels)
    {
        for (var i = 0; i < levels.Length; i++)
        {
            yield return CreateItem((byte)(InventoryConstants.HelmSlot + i), (byte)(7 + i), 17, levels[i]);
        }
    }

    private static Item CreateItem(byte slot, byte group, short number, byte level)
    {
        var definition = new Mock<ItemDefinition>();
        definition.SetupAllProperties();
        definition.Object.Group = group;
        definition.Object.Number = number;
        definition.Object.Durability = 100;
        var item = new Mock<Item>();
        item.SetupAllProperties();
        item.Object.Definition = definition.Object;
        item.Object.ItemSlot = slot;
        item.Object.Level = level;
        item.Object.Durability = 100;
        return item.Object;
    }

    private static AttributeSystem GetAttributeSystem() => new(Enumerable.Empty<IAttribute>(), Enumerable.Empty<IAttribute>(), Enumerable.Empty<AttributeRelationship>());
}
