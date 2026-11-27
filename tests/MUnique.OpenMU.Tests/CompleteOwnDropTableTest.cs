// <copyright file="CompleteOwnDropTableTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Tests that a monster whose own drop chances add up to 100% drops only from its own table (the dark skeletons:
/// Box of Heaven 90%, a chocolate box 10%), and that other monsters keep the drops of the map.
/// </summary>
[TestFixture]
public class CompleteOwnDropTableTest
{
    private const byte MonsterLevel = 100;

    /// <summary>
    /// Tests that the first 90% of the random range drops the first box and the rest the second, with no money and no
    /// drop of the map, although the map has groups which would be picked otherwise.
    /// </summary>
    /// <param name="random">The random value of the drop.</param>
    /// <param name="expectBoxOfHeaven">Whether the Box of Heaven is expected, otherwise the chocolate box.</param>
    [TestCase(0.0, true)]
    [TestCase(0.5, true)]
    [TestCase(0.89, true)]
    [TestCase(0.91, false)]
    [TestCase(0.999, false)]
    public async ValueTask DropsOnlyOwnBoxesAsync(double random, bool expectBoxOfHeaven)
    {
        var boxOfLuck = CreateItemDefinition(14, 11);
        var chocolate = CreateItemDefinition(14, 32);
        var mapItem = CreateItemDefinition(14, 13);
        var monster = CreateMonster();
        monster.DropItemGroups.Add(CreateGroup(0.9, 7, boxOfLuck, monster));
        monster.DropItemGroups.Add(CreateGroup(0.1, 0, chocolate, monster));

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(CreateGroup(0.99, 0, mapItem, null));
        player.CurrentMap!.Definition.DropItemGroups.Add(CreateMoneyGroup());

        var generator = new DefaultDropGenerator(CreateGameConfig(), CreateRandomizer(random), smallBonusDropsEnabled: true);
        var (items, money) = await generator.GenerateItemDropsAsync(monster, 1000, player).ConfigureAwait(false);

        var item = items.Single();
        Assert.That(money, Is.Null);
        if (expectBoxOfHeaven)
        {
            Assert.That(item.Definition, Is.EqualTo(boxOfLuck));
            Assert.That(item.Level, Is.EqualTo(7));
        }
        else
        {
            Assert.That(item.Definition, Is.EqualTo(chocolate));
            Assert.That(item.Level, Is.EqualTo(0));
        }
    }

    /// <summary>
    /// Tests that a monster whose own chances do not add up to 100% still gets the drops of the map.
    /// </summary>
    [Test]
    public async ValueTask IncompleteOwnTableKeepsMapDropsAsync()
    {
        var ownItem = CreateItemDefinition(14, 11);
        var mapItem = CreateItemDefinition(14, 13);
        var monster = CreateMonster();
        monster.DropItemGroups.Add(CreateGroup(0.5, 0, ownItem, monster));

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(CreateGroup(0.5, 0, mapItem, null));

        // 0.75 is after the own 50%, so it lands in the group of the map.
        var generator = new DefaultDropGenerator(CreateGameConfig(), CreateRandomizer(0.75));
        var (items, _) = await generator.GenerateItemDropsAsync(monster, 1, player).ConfigureAwait(false);

        Assert.That(items.Single().Definition, Is.EqualTo(mapItem));
    }

    /// <summary>
    /// Tests which own drop tables count as complete.
    /// </summary>
    [Test]
    public void CompleteOnlyWhenChancesBelowOneAddUpToOne()
    {
        var item = CreateItemDefinition(14, 11);

        var none = CreateMonster();
        Assert.That(DefaultDropGenerator.HasCompleteOwnDropTable(none), Is.False, "no own groups");

        var half = CreateMonster();
        half.DropItemGroups.Add(CreateGroup(0.5, 0, item, half));
        Assert.That(DefaultDropGenerator.HasCompleteOwnDropTable(half), Is.False, "50%");

        var guaranteed = CreateMonster();
        guaranteed.DropItemGroups.Add(CreateGroup(1.0, 0, item, guaranteed));
        Assert.That(DefaultDropGenerator.HasCompleteOwnDropTable(guaranteed), Is.False, "a guaranteed group");

        var smallBonus = CreateMonster();
        var bonusGroup = CreateGroup(0.9, 0, item, smallBonus);
        bonusGroup.ItemType = SpecialItemType.SmallBonus;
        smallBonus.DropItemGroups.Add(bonusGroup);
        smallBonus.DropItemGroups.Add(CreateGroup(0.1, 0, item, smallBonus));
        Assert.That(DefaultDropGenerator.HasCompleteOwnDropTable(smallBonus), Is.False, "a small bonus group");

        var complete = CreateMonster();
        complete.DropItemGroups.Add(CreateGroup(0.9, 7, item, complete));
        complete.DropItemGroups.Add(CreateGroup(0.1, 0, item, complete));
        Assert.That(DefaultDropGenerator.HasCompleteOwnDropTable(complete), Is.True, "90% + 10%");
    }

    private static MonsterDefinition CreateMonster()
    {
        var monster = new Mock<MonsterDefinition>();
        monster.SetupAllProperties();
        monster.Setup(m => m.DropItemGroups).Returns(new List<DropItemGroup>());
        monster.Setup(m => m.Attributes).Returns(new List<MonsterAttribute>());
        monster.Object.NumberOfMaximumItemDrops = 1;
        monster.Object.ObjectKind = NpcObjectKind.Monster;
        monster.Object.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = MonsterLevel });
        return monster.Object;
    }

    private static DropItemGroup CreateGroup(double chance, byte itemLevel, ItemDefinition item, MonsterDefinition? monster)
    {
        var group = new Mock<DropItemGroup>();
        group.SetupAllProperties();
        group.Object.Chance = chance;
        group.Object.ItemLevel = itemLevel;
        group.Object.ItemType = SpecialItemType.None;
        group.Object.Monster = monster;
        group.Setup(g => g.PossibleItems).Returns(new List<ItemDefinition> { item });
        return group.Object;
    }

    private static DropItemGroup CreateMoneyGroup()
    {
        var group = new Mock<DropItemGroup>();
        group.SetupAllProperties();
        group.Object.Chance = 1.0;
        group.Object.ItemType = SpecialItemType.Money;
        group.Setup(g => g.PossibleItems).Returns(new List<ItemDefinition>());
        return group.Object;
    }

    private static ItemDefinition CreateItemDefinition(byte group, short number)
    {
        var itemDefinition = new Mock<ItemDefinition>();
        itemDefinition.SetupAllProperties();
        itemDefinition.Object.Group = group;
        itemDefinition.Object.Number = number;
        itemDefinition.Object.Durability = 1;
        itemDefinition.Object.MaximumItemLevel = 15;
        itemDefinition.Setup(d => d.PossibleItemOptions).Returns(new List<ItemOptionDefinition>());
        return itemDefinition.Object;
    }

    private static GameConfiguration CreateGameConfig()
    {
        var gameConfiguration = new Mock<GameConfiguration>();
        gameConfiguration.Setup(c => c.Items).Returns(new List<ItemDefinition>());
        return gameConfiguration.Object;
    }

    private static IRandomizer CreateRandomizer(double value)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(0);
        randomizer.Setup(r => r.NextDouble()).Returns(value);
        return randomizer.Object;
    }
}
