// <copyright file="DropGeneratorTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Premium;

/// <summary>
/// Tests the drop generator.
/// </summary>
[TestFixture]
public class DropGeneratorTest
{
    /// <summary>
    /// Tests if the drop fails because the randomizer returns a number which causes a fail.
    /// </summary>
    [Test]
    public async ValueTask TestDropFailAsync()
    {
        var config = this.GetGameConfig();
        var generator = new DefaultDropGenerator(config, this.GetRandomizer(9999));
        var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 0), 0, await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false));
        var item = items.FirstOrDefault();
        Assert.That(item, Is.Null);
    }

    /// <summary>
    /// Tests the drops defined by a monster are getting considered.
    /// </summary>
    [Test]
    public async ValueTask TestItemDropItemByMonsterAsync()
    {
        var config = this.GetGameConfig();
        var monster = this.GetMonster(1, 0);
        monster.DropItemGroups.AddBasicDropItemGroups();
        monster.DropItemGroups.Add(3000, SpecialItemType.RandomItem, true);

        var generator = new DefaultDropGenerator(config, this.GetRandomizer2(0, 0.5));
        var (items, _) = await generator.GenerateItemDropsAsync(monster, 1, await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false));
        var item = items.FirstOrDefault();

        Assert.That(item, Is.Not.Null);

        // ReSharper disable once PossibleNullReferenceException
        Assert.That(item!.Definition, Is.EqualTo(monster.DropItemGroups.Last().PossibleItems.First()));
    }

    /// <summary>
    /// Tests that items with a maximum drop level are filtered from generic monster drops.
    /// </summary>
    [Test]
    public async ValueTask TestMaximumDropLevelAsync()
    {
        var config = this.GetGameConfig();
        var cappedItem = this.CreateItemDefinition(12, 15, 12, 66);
        var uncappedItem = this.CreateItemDefinition(14, 13, 25);

        var dropGroup = new Mock<DropItemGroup>();
        dropGroup.SetupAllProperties();
        dropGroup.Object.Chance = 1.0;
        dropGroup.Object.ItemType = SpecialItemType.Jewel;
        dropGroup.Setup(g => g.PossibleItems).Returns(new List<ItemDefinition> { cappedItem, uncappedItem });

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(dropGroup.Object);

        var generator = new DefaultDropGenerator(config, this.GetRandomizer(0));
        var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 67), 1, player).ConfigureAwait(false);
        var item = items.FirstOrDefault();

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Definition, Is.EqualTo(uncappedItem));
    }

    /// <summary>
    /// Tests that potions and the apple never drop above level 0, while other items keep the group's level.
    /// </summary>
    /// <param name="number">The item number in group 14.</param>
    /// <param name="expectedLevel">The expected level of the dropped item.</param>
    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(6, 0)]
    [TestCase(35, 0)]
    [TestCase(40, 0)]
    [TestCase(15, 1)]
    public async ValueTask TestPotionsDropWithoutLevelAsync(short number, byte expectedLevel)
    {
        var config = this.GetGameConfig();
        var definition = this.CreateItemDefinition(14, number, 1);
        definition.MaximumItemLevel = 4;

        var dropGroup = new Mock<DropItemGroup>();
        dropGroup.SetupAllProperties();
        dropGroup.Object.Chance = 1.0;
        dropGroup.Object.ItemType = SpecialItemType.Jewel;
        dropGroup.Object.ItemLevel = 1;
        dropGroup.Setup(g => g.PossibleItems).Returns(new List<ItemDefinition> { definition });

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(dropGroup.Object);

        var generator = new DefaultDropGenerator(config, this.GetRandomizer(0));
        var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 70), 1, player).ConfigureAwait(false);
        var item = items.FirstOrDefault();

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Level, Is.EqualTo(expectedLevel));
    }

    /// <summary>
    /// Tests the drops defined by a player are getting considered.
    /// </summary>
    public void TestItemDropItemByPlayer()
    {
        // to be implemented
    }

    /// <summary>
    /// Tests the drops defined by a map are getting considered.
    /// </summary>
    public void TestItemDropItemByMap()
    {
        // to be implemented
    }

    /// <summary>
    /// Tests that ExcellentItemDropLevelDelta property exists and has correct default.
    /// </summary>
    [Test]
    public void TestExcellentItemDropLevelDelta_PropertyExists()
    {
        var config = this.GetGameConfig();
        // The initializer sets default to 25 for backward compatibility
        config.ExcellentItemDropLevelDelta = 25;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(25));

        config.ExcellentItemDropLevelDelta = 0;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(0));

        config.ExcellentItemDropLevelDelta = 50;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(50));
    }

    /// <summary>
    /// Tests that the combined bonus chance is independent of ordinary loot weights and slots.
    /// </summary>
    /// <param name="roll">The bonus roll.</param>
    /// <param name="slots">The normal loot slots.</param>
    /// <param name="expectedBonuses">The expected bonus count.</param>
    /// <param name="premiumBonus">The active premium drop bonus.</param>
    [TestCase(0.04999, 1, 1, 0)]
    [TestCase(0.05, 1, 0, 0)]
    [TestCase(0.04999, 1, 1, 0.5f)]
    [TestCase(0.05001, 1, 0, 0.5f)]
    [TestCase(0.05001, 1, 0, 0)]
    [TestCase(0.04999, 3, 1, 0)]
    public async Task TestIndependentSmallBonusAsync(double roll, int slots, int expectedBonuses, float premiumBonus)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes!.AddElement(new SimpleElement(premiumBonus, AggregateType.AddRaw), PremiumBuffStats.ItemDropBonus);
        var groups = player.CurrentMap!.Definition.DropItemGroups;
        groups.Add(9000, SpecialItemType.None, true);
        groups.Add(9000, SpecialItemType.None, true);
        var bonus = groups.Add(500, SpecialItemType.SmallBonus, true);
        bonus.ItemLevel = 5;
        bonus.PossibleItems.First().MaximumItemLevel = 15;
        var generator = new DefaultDropGenerator(this.GetGameConfig(), this.GetRandomizer2(0, roll));

        var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(slots, 70), 1, player).ConfigureAwait(false);
        var result = items.ToList();
        Assert.That(result.Count, Is.EqualTo(slots + expectedBonuses));
        Assert.That(result.Count(i => i.Definition == bonus.PossibleItems.First()), Is.EqualTo(expectedBonuses));
        Assert.That(result.Where(i => i.Definition == bonus.PossibleItems.First()).All(i => i.Level == 5), Is.True);
    }

    /// <summary>
    /// Tests that guaranteed rewards, zero-drop monsters and other NPCs do not receive bonuses.
    /// </summary>
    /// <param name="kind">The object kind.</param>
    /// <param name="slots">The drop slots.</param>
    /// <param name="guaranteed">Whether a guaranteed reward exists.</param>
    [TestCase(NpcObjectKind.Monster, 0, false)]
    [TestCase(NpcObjectKind.Monster, 2, true)]
    [TestCase(NpcObjectKind.Destructible, 1, false)]
    [TestCase(NpcObjectKind.Guard, 1, false)]
    public async Task TestSmallBonusExclusionsAsync(NpcObjectKind kind, int slots, bool guaranteed)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var bonus = player.CurrentMap!.Definition.DropItemGroups.Add(500, SpecialItemType.SmallBonus, true);
        var monster = this.GetMonster(slots, 70);
        monster.ObjectKind = kind;
        if (guaranteed)
        {
            monster.DropItemGroups.Add(10000, SpecialItemType.None, true);
        }

        var generator = new DefaultDropGenerator(this.GetGameConfig(), this.GetRandomizer(0));
        var (items, _) = await generator.GenerateItemDropsAsync(monster, 1, player).ConfigureAwait(false);
        Assert.That(items.Any(i => i.Definition == bonus.PossibleItems.First()), Is.False);
    }

    /// <summary>
    /// Tests filtering and clearing the reusable bonus pool between kills and maps.
    /// </summary>
    [Test]
    public async Task TestSmallBonusFiltersAndReuseAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var groups = player.CurrentMap!.Definition.DropItemGroups;
        var bonus = groups.Add(500, SpecialItemType.SmallBonus, true);
        bonus.MinimumMonsterLevel = 50;
        var generator = new DefaultDropGenerator(this.GetGameConfig(), this.GetRandomizer(0));
        var (lowItems, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 49), 1, player).ConfigureAwait(false);
        Assert.That(lowItems, Is.Empty);
        var (eligibleItems, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 50), 1, player).ConfigureAwait(false);
        Assert.That(eligibleItems.Count(), Is.EqualTo(1));
        groups.Remove(bonus);
        var (nextItems, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 50), 1, player).ConfigureAwait(false);
        Assert.That(nextItems, Is.Empty);
    }

    /// <summary>
    /// Tests all nineteen equal shares over a deterministic grid of ten thousand rolls.
    /// </summary>
    [Test]
    public async Task TestSmallBonusCombinedFivePercentAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var groups = player.CurrentMap!.Definition.DropItemGroups;
        for (var i = 0; i < 19; i++)
        {
            groups.Add(1, SpecialItemType.SmallBonus, true).Chance = 0.05 / 19;
        }

        double roll = 0;
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextDouble()).Returns(() => roll);
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(0);
        var generator = new DefaultDropGenerator(this.GetGameConfig(), randomizer.Object);
        var monster = this.GetMonster(3, 70);
        var hits = 0;
        var variants = new HashSet<ItemDefinition>();
        for (var i = 0; i < 10000; i++)
        {
            roll = (i + 0.5) / 10000;
            var (items, _) = await generator.GenerateItemDropsAsync(monster, 1, player).ConfigureAwait(false);
            var result = items.ToList();
            Assert.That(result.Count, Is.LessThanOrEqualTo(1));
            hits += result.Count;
            foreach (var item in result)
            {
                variants.Add(item.Definition!);
            }
        }

        Assert.That(hits, Is.EqualTo(500));
        Assert.That(variants.Count, Is.EqualTo(19));
    }

    private MonsterDefinition GetMonster(int numberOfDrops, byte level)
    {
        var monster = new Mock<MonsterDefinition>();
        monster.SetupAllProperties();
        monster.Setup(m => m.DropItemGroups).Returns(new List<DropItemGroup>());
        monster.Setup(m => m.Attributes).Returns(new List<MonsterAttribute>());
        monster.Object.NumberOfMaximumItemDrops = numberOfDrops;
        monster.Object.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = level });
        return monster.Object;
    }

    private IRandomizer GetRandomizer(int randomValue)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(randomValue);
        randomizer.Setup(r => r.NextDouble()).Returns(randomValue / 10000.0);
        return randomizer.Object;
    }

    private IRandomizer GetRandomizer2(int integerValue, double doubleValue)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(integerValue);
        randomizer.Setup(r => r.NextDouble()).Returns(doubleValue);

        return randomizer.Object;
    }

    private GameConfiguration GetGameConfig()
    {
        var gameConfiguration = new Mock<GameConfiguration>();
        gameConfiguration.Setup(c => c.Items).Returns(new List<ItemDefinition>());
        return gameConfiguration.Object;
    }

    private ItemDefinition CreateItemDefinition(byte group, short number, byte dropLevel, byte? maximumDropLevel = null)
    {
        var itemDefinition = new Mock<ItemDefinition>();
        itemDefinition.SetupAllProperties();
        itemDefinition.Object.Group = group;
        itemDefinition.Object.Number = number;
        itemDefinition.Object.DropLevel = dropLevel;
        itemDefinition.Object.MaximumDropLevel = maximumDropLevel;
        itemDefinition.Object.Durability = 1;
        itemDefinition.Setup(d => d.PossibleItemOptions).Returns(new List<ItemOptionDefinition>());
        return itemDefinition.Object;
    }
}
