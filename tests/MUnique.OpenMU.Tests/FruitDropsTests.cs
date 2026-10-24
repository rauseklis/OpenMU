// <copyright file="FruitDropsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;
using Basic = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests that the monsters of Arena, Icarus, Aida and Kanturu drop all five fruits.
/// </summary>
[TestFixture]
public class FruitDropsTests
{
    private static readonly short[] FruitMapNumbers = [6, 10, 33, 37, 38, 39];

    private static readonly short[] OtherMapNumbers = [0, 1, 2, 3, 7, 8, 40];

    /// <summary>
    /// Tests that the update gives each of the six maps one group per fruit and no other map gets any.
    /// </summary>
    [Test]
    public async Task UpdateAddsAllFiveFruitsToTheSixMapsOnly()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = CreateConfiguration(context);

        await new AddFruitDropsUpdatePlugIn().ApplyUpdateAsync(context, configuration);

        var fruit = configuration.Items.Single();
        foreach (var map in configuration.Maps.Where(m => FruitMapNumbers.Contains(m.Number)))
        {
            Assert.That(map.DropItemGroups.Count, Is.EqualTo(5), $"map {map.Number}");
            Assert.That(map.DropItemGroups.Select(g => g.ItemLevel), Is.EquivalentTo(new byte?[] { 0, 1, 2, 3, 4 }), $"map {map.Number}");
            Assert.That(map.DropItemGroups.All(g => g.Chance == FruitDropGroups.ChancePerFruit), Is.True);
            Assert.That(map.DropItemGroups.All(g => g.PossibleItems.Single() == fruit), Is.True);
        }

        foreach (var map in configuration.Maps.Where(m => OtherMapNumbers.Contains(m.Number)))
        {
            Assert.That(map.DropItemGroups, Is.Empty, $"map {map.Number}");
        }
    }

    /// <summary>
    /// Tests that applying it a second time changes nothing, so no group is ever added twice.
    /// </summary>
    [Test]
    public async Task ApplyingTwiceDoesNotAddGroupsTwice()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = CreateConfiguration(context);

        FruitDropGroups.EnsureFor(context, configuration);
        FruitDropGroups.EnsureFor(context, configuration);
        await new AddFruitDropsUpdatePlugIn().ApplyUpdateAsync(context, configuration);

        Assert.That(configuration.DropItemGroups.Count, Is.EqualTo(5));
        Assert.That(configuration.Maps.Single(m => m.Number == 37).DropItemGroups.Count, Is.EqualTo(5));
    }

    /// <summary>
    /// Tests that the update lowers fruit drops which were created with the first, three times higher chance.
    /// </summary>
    [Test]
    public async Task LowerChanceUpdateReplacesTheFirstChanceAndAddsNothing()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = CreateConfiguration(context);
        FruitDropGroups.EnsureFor(context, configuration);
        foreach (var group in configuration.DropItemGroups)
        {
            group.Chance = 0.03;
            group.Description = $"Fruit of {group.ItemLevel} (3 % drop chance)";
        }

        await new LowerFruitDropChanceUpdatePlugIn().ApplyUpdateAsync(context, configuration);

        Assert.That(configuration.DropItemGroups.Count, Is.EqualTo(5));
        Assert.That(configuration.DropItemGroups.All(g => g.Chance == 0.01), Is.True);
        Assert.That(configuration.DropItemGroups.All(g => (g.Description.ToString() ?? string.Empty).Contains("(1 % drop chance)")), Is.True);
        Assert.That(configuration.Maps.Single(m => m.Number == 10).DropItemGroups.Count, Is.EqualTo(5));
    }

    /// <summary>
    /// Tests that a brand-new Season 6 database already has the fruit drops on exactly those six maps.
    /// </summary>
    [Test]
    public async Task NewSeasonSixDatabaseHasTheFruitDrops()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(contextProvider, new NullLoggerFactory()).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        var configuration = (await contextProvider.CreateNewConfigurationContext().GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        var withFruits = configuration.Maps
            .Where(m => m.DropItemGroups.Count(g => g.PossibleItems.Any(i => i.Group == 13 && i.Number == 15)) == 5)
            .Select(m => (int)m.Number)
            .OrderBy(n => n)
            .ToList();
        var withAnyFruit = configuration.Maps
            .Where(m => m.DropItemGroups.Any(g => g.PossibleItems.Any(i => i.Group == 13 && i.Number == 15)))
            .Select(m => (int)m.Number)
            .OrderBy(n => n)
            .ToList();

        Assert.That(withFruits, Is.EqualTo(new[] { 6, 10, 33, 37, 38, 39 }));
        Assert.That(withAnyFruit, Is.EqualTo(withFruits), "no other map may drop fruits");
    }

    /// <summary>
    /// Tests that a monster on one of these maps really drops the matching fruit, at the level which is its type.
    /// </summary>
    /// <param name="level">The fruit level: 0 Energy, 1 Vitality, 2 Agility, 3 Strength, 4 Command.</param>
    /// <param name="roll">A random roll which falls into that fruit's slice of the drop chance.</param>
    [TestCase(0, 0.005)]
    [TestCase(1, 0.015)]
    [TestCase(2, 0.025)]
    [TestCase(3, 0.035)]
    [TestCase(4, 0.045)]
    public async Task MonsterDropsTheFruitOfTheRolledType(byte level, double roll)
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = CreateConfiguration(context);
        FruitDropGroups.EnsureFor(context, configuration);

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        foreach (var group in configuration.Maps.Single(m => m.Number == 33).DropItemGroups)
        {
            player.CurrentMap!.Definition.DropItemGroups.Add(group);
        }

        var generator = new DefaultDropGenerator(GetGameConfig(), GetRandomizer(roll));
        var (items, _) = await generator.GenerateItemDropsAsync(GetMonster(1, 60), 1, player).ConfigureAwait(false);
        var item = items.SingleOrDefault();

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Definition, Is.SameAs(configuration.Items.Single()));
        Assert.That(item.Level, Is.EqualTo(level));
    }

    /// <summary>
    /// Tests that a roll above the fruit chances drops nothing, so only 5 % of the drops are fruits.
    /// </summary>
    [Test]
    public async Task MonsterDropsNoFruitAboveTheTotalChance()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = CreateConfiguration(context);
        FruitDropGroups.EnsureFor(context, configuration);

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        foreach (var group in configuration.Maps.Single(m => m.Number == 6).DropItemGroups)
        {
            player.CurrentMap!.Definition.DropItemGroups.Add(group);
        }

        var generator = new DefaultDropGenerator(GetGameConfig(), GetRandomizer(0.5));
        var (items, _) = await generator.GenerateItemDropsAsync(GetMonster(1, 60), 1, player).ConfigureAwait(false);

        Assert.That(items, Is.Empty);
    }

    private static Basic.GameConfiguration CreateConfiguration(IContext context)
    {
        var configuration = context.CreateNew<Basic.GameConfiguration>();
        var fruit = context.CreateNew<Basic.ItemDefinition>();
        fruit.Group = FruitDropGroups.FruitItemGroup;
        fruit.Number = FruitDropGroups.FruitItemNumber;
        fruit.Name = "Fruits";
        fruit.MaximumItemLevel = 4;
        fruit.Durability = 1;
        configuration.Items.Add(fruit);

        foreach (var number in FruitMapNumbers.Concat(OtherMapNumbers))
        {
            var map = context.CreateNew<Basic.GameMapDefinition>();
            map.Number = number;
            map.Discriminator = 0;
            configuration.Maps.Add(map);
        }

        return configuration;
    }

    private static MonsterDefinition GetMonster(int numberOfDrops, byte level)
    {
        var monster = new Mock<MonsterDefinition>();
        monster.SetupAllProperties();
        monster.Setup(m => m.DropItemGroups).Returns(new List<DropItemGroup>());
        monster.Setup(m => m.Attributes).Returns(new List<MonsterAttribute>());
        monster.Object.NumberOfMaximumItemDrops = numberOfDrops;
        monster.Object.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = level });
        return monster.Object;
    }

    private static IRandomizer GetRandomizer(double roll)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(0);
        randomizer.Setup(r => r.NextDouble()).Returns(roll);
        return randomizer.Object;
    }

    private static GameConfiguration GetGameConfig()
    {
        var gameConfiguration = new Mock<GameConfiguration>();
        gameConfiguration.Setup(c => c.Items).Returns(new List<MUnique.OpenMU.DataModel.Configuration.Items.ItemDefinition>());
        return gameConfiguration.Object;
    }
}
