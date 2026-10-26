// <copyright file="AbsoluteBonusProgressionTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

/// <summary>
/// Pins absolute monster chances and the fruit-free Heaven pool.
/// </summary>
[TestFixture]
public class AbsoluteBonusProgressionTests
{
    private GameConfiguration _configuration = null!;
    private GameServerDefinition _server = null!;

    [OneTimeSetUp]
    public async Task InitializeAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        this._configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        this._server = (await context.GetAsync<GameServerDefinition>().ConfigureAwait(false)).Single(server => server.ServerID == 0);
        if (Environment.GetEnvironmentVariable("ABSOLUTE_BONUS_CATALOG_PATH") is { } path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this._configuration.Maps.Select(map => new
            {
                map.Number,
                Name = map.Name.Value,
                Spawns = map.MonsterSpawns.Where(spawn => spawn.MonsterDefinition?.Attributes.Any(attribute => attribute.AttributeDefinition == Stats.Level && attribute.Value <= 44) is true)
                    .Select(spawn => new { Monster = spawn.MonsterDefinition!.Number, Name = spawn.MonsterDefinition.Designation.Value, Level = spawn.MonsterDefinition[Stats.Level], spawn.SpawnTrigger, spawn.MonsterDefinition.ObjectKind, spawn.MonsterDefinition.NumberOfMaximumItemDrops }).Distinct(),
            }), new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Test]
    public void MainServerUsesAbsoluteGroupChances()
    {
        Assert.That(this._server.SmallBonusDropsEnabled, Is.True);
        Assert.That(this._server.SmallBonusDropChance, Is.Null, "No combined 5% override may control the configured bonuses.");
    }

    [Test]
    public void EveryBonusUsesItsAbsoluteChance()
    {
        var chances = new Dictionary<byte, double> { [5] = .01, [6] = .005, [3] = .0035, [7] = .0025, [2] = .0015, [1] = .0005 };
        foreach (var group in this._configuration.Maps.SelectMany(map => map.DropItemGroups).Where(group => group.ItemType == SpecialItemType.SmallBonus))
        {
            Assert.That(group.Chance, Is.EqualTo(chances[group.ItemLevel!.Value]).Within(1e-12), group.Description);
        }
    }

    [Test]
    public void HeavenHasFourCategoriesAndNoFruit()
    {
        var groups = this._configuration.Items.Single(item => item.Group == 14 && item.Number == 11).DropItems.Where(group => group.SourceItemLevel == 7).ToArray();
        Assert.That(groups, Has.Length.EqualTo(4));
        Assert.That(groups.Sum(group => group.Chance), Is.EqualTo(1).Within(1e-12));
        Assert.That(groups.SelectMany(group => group.PossibleItems).Any(item => item.Group == 13 && item.Number == 15), Is.False);
        Assert.That(groups.Single(group => group.PossibleItems.Any(item => item.Group == 14 && item.Number == 13)).Chance, Is.EqualTo(.45));
        Assert.That(groups.Single(group => group.PossibleItems.Any(item => item.Group == 13 && item.Number == 1)).Chance, Is.EqualTo(.30));
        Assert.That(groups.Single(group => group.PossibleItems.Any(item => item.Group == 13 && item.Number == 64)).Chance, Is.EqualTo(.05));
        Assert.That(groups.Single(group => group.ItemType == SpecialItemType.Money).Chance, Is.EqualTo(.20));
    }

    [Test]
    public void SilverUsesOnlyEligibleAutomaticSpawnsAndInclusiveBounds()
    {
        var pairs = this._configuration.Maps.SelectMany(map => map.DropItemGroups.Where(group => group.ItemType == SpecialItemType.SmallBonus && group.ItemLevel == 5)
            .Select(group => (Map: map, Group: group))).ToArray();
        Assert.That(pairs, Is.Not.Empty);
        Assert.That(pairs.Select(pair => (pair.Map, pair.Group.Monster)).Distinct().Count(), Is.EqualTo(pairs.Length));
        foreach (var pair in pairs)
        {
            var monster = pair.Group.Monster!;
            Assert.That(monster[Stats.Level], Is.InRange(1, 44));
            Assert.That(monster.ObjectKind, Is.EqualTo(NpcObjectKind.Monster));
            Assert.That(pair.Map.MonsterSpawns.Any(spawn => spawn.MonsterDefinition == monster && spawn.SpawnTrigger == SpawnTrigger.Automatic && spawn.Quantity > 0), Is.True);
            Assert.That(pair.Group.MinimumMonsterLevel, Is.EqualTo(1));
            Assert.That(pair.Group.MaximumMonsterLevel, Is.EqualTo(44));
        }

        Assert.That(pairs.Any(pair => pair.Group.Monster![Stats.Level] == 44), Is.True);
        Assert.That(pairs.Any(pair => pair.Map.Number == 0), Is.True, "Early Lorencia");
        Assert.That(pairs.Any(pair => pair.Map.Number == 3), Is.True, "Early Noria");
        Assert.That(pairs.Any(pair => pair.Map.Number == 51), Is.True, "Early Elvenland");
        Assert.That(pairs.Any(pair => pair.Map.Number == 7 && pair.Group.Monster!.Number == 45), Is.True, "Bahamut overlap");
    }

    [Test]
    public void SilverRejectsEventSpawnsNpcsBossesAndLevelsOutsideRange()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var context = provider.CreateNewContext();
        var map = this._configuration.Maps.Single(map => map.Number == 0);
        var eventMap = this._configuration.MiniGameDefinitions.Select(game => game.Entrance?.Map).First(map => map is not null)!;
        var spawns = new List<MonsterSpawnArea>();
        var levels = new[] { 1, 44, 45, 0, 20, 20, 20, 20 };
        for (var index = 0; index < levels.Length; index++)
        {
            var monster = context.CreateNew<MonsterDefinition>();
            monster.Number = (short)(6000 + index);
            monster.Designation = $"Silver eligibility probe {index}";
            monster.ObjectKind = index == 4 ? NpcObjectKind.PassiveNpc : NpcObjectKind.Monster;
            monster.NumberOfMaximumItemDrops = 1;
            var level = context.CreateNew<MonsterAttribute>();
            level.AttributeDefinition = Stats.Level;
            level.Value = levels[index];
            monster.Attributes.Add(level);
            if (index == 6)
            {
                var guaranteed = context.CreateNew<DropItemGroup>();
                guaranteed.Chance = 1;
                monster.DropItemGroups.Add(guaranteed);
            }

            var spawn = context.CreateNew<MonsterSpawnArea>();
            spawn.MonsterDefinition = monster;
            spawn.Quantity = index == 7 ? (short)0 : (short)1;
            spawn.SpawnTrigger = index == 5 ? SpawnTrigger.OnceAtEventStart : SpawnTrigger.Automatic;
            map.MonsterSpawns.Add(spawn);
            spawns.Add(spawn);
        }

        eventMap.MonsterSpawns.Add(spawns[0]);
        try
        {
            new AbsoluteBonusProgression(context, this._configuration).Initialize();
            var silver = map.DropItemGroups.Where(group => group.ItemType == SpecialItemType.SmallBonus && group.ItemLevel == 5).ToArray();
            Assert.That(silver.Any(group => group.Monster == spawns[0].MonsterDefinition), Is.True, "Level 1 inclusive");
            Assert.That(silver.Any(group => group.Monster == spawns[1].MonsterDefinition), Is.True, "Level 44 inclusive");
            Assert.That(silver.Any(group => spawns.Skip(2).Any(spawn => spawn.MonsterDefinition == group.Monster)), Is.False);
            Assert.That(eventMap.DropItemGroups.Any(group => group.ItemType == SpecialItemType.SmallBonus && group.ItemLevel == 5), Is.False);
        }
        finally
        {
            foreach (var spawn in spawns)
            {
                map.MonsterSpawns.Remove(spawn);
            }

            eventMap.MonsterSpawns.Remove(spawns[0]);
            new AbsoluteBonusProgression(context, this._configuration).Initialize();
        }
    }

    [Test]
    public void ExistingNonSilverAssignmentsMatchReviewedResource()
    {
        using var stream = typeof(BonusProgression).Assembly.GetManifestResourceStream("MUnique.OpenMU.Persistence.Initialization.Resources.BonusProgression.json")!;
        using var plan = JsonDocument.Parse(stream);
        var expected = plan.RootElement.GetProperty("bonuses").EnumerateArray().Where(bonus => bonus.GetProperty("level").GetInt32() != 5)
            .Select(bonus => (bonus.GetProperty("map").GetInt32(), bonus.GetProperty("monster").GetInt32(), bonus.GetProperty("level").GetInt32())).ToArray();
        var actual = this._configuration.Maps.SelectMany(map => map.DropItemGroups.Where(group => group.ItemType == SpecialItemType.SmallBonus && group.ItemLevel != 5)
            .Select(group => ((int)map.Number, (int)group.Monster!.Number, (int)group.ItemLevel!.Value))).ToArray();
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void EventRewardGroupsAreNeverRetunedOrRemoved()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var context = provider.CreateNewContext();
        var game = this._configuration.MiniGameDefinitions.First();
        var group = context.CreateNew<DropItemGroup>();
        group.ItemType = SpecialItemType.SmallBonus;
        group.ItemLevel = 5;
        group.Chance = .75;
        group.PossibleItems.Add(this._configuration.Items.Single(item => item.Group == 14 && item.Number == 11));
        var reward = context.CreateNew<MiniGameReward>();
        reward.ItemReward = group;
        game.Rewards.Add(reward);
        this._configuration.DropItemGroups.Add(group);
        try
        {
            new AbsoluteBonusProgression(context, this._configuration).Initialize();
            Assert.That(group.Chance, Is.EqualTo(.75));
            Assert.That(this._configuration.DropItemGroups, Does.Contain(group));
            Assert.That(reward.ItemReward, Is.SameAs(group));
        }
        finally
        {
            game.Rewards.Remove(reward);
            this._configuration.DropItemGroups.Remove(group);
        }
    }

    [Test]
    public void ReapplyPreservesEveryUnrelatedOpeningPoolAndExchange()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var context = provider.CreateNewContext();
        var before = UnrelatedFingerprint(this._configuration);
        new AbsoluteBonusProgression(context, this._configuration).Initialize();
        new AbsoluteBonusProgression(context, this._configuration).Initialize();
        Assert.That(UnrelatedFingerprint(this._configuration), Is.EqualTo(before));
    }

    [Test]
    public void OneHundredThousandHeavenOpeningsHaveNoFruitAndCorrectWeights()
    {
        const int samples = 100_000;
        var groups = this._configuration.Items.Single(item => item.Group == 14 && item.Number == 11).DropItems.Where(group => group.SourceItemLevel == 7).ToArray();
        var generator = new DefaultDropGenerator(this._configuration, new SeededRandomizer(26151));
        var counts = new Dictionary<(int Group, int Number), int>();
        var zen = 0;
        for (var draw = 0; draw < samples; draw++)
        {
            var result = generator.GenerateItemDrop(groups);
            if (result.Money is { } amount)
            {
                Assert.That(amount, Is.EqualTo(100_000));
                Assert.That(result.Item, Is.Null);
                zen++;
                continue;
            }

            Assert.That(result.Item, Is.Not.Null);
            var key = ((int)result.Item!.Definition!.Group, (int)result.Item.Definition.Number);
            Assert.That(key, Is.Not.EqualTo((13, 15)));
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        foreach (var (key, chance) in new[] { ((14, 13), .225), ((14, 14), .225), ((13, 1), .10), ((13, 0), .10), ((13, 2), .10), ((13, 64), .05 / 3), ((13, 65), .05 / 3), ((13, 3), .05 / 3) })
        {
            Assert.That((double)counts[key] / samples, Is.EqualTo(chance).Within(.005));
        }

        Assert.That((double)zen / samples, Is.EqualTo(.20).Within(.005));
        TestContext.WriteLine($"Heaven: Zen {zen}, items {string.Join(',', counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"))} of {samples}.");
    }

    private static string UnrelatedFingerprint(GameConfiguration configuration) => string.Join('\n', configuration.Items.OrderBy(item => item.Group).ThenBy(item => item.Number)
        .SelectMany(item => item.DropItems.Where(group => !(item.Group == 14 && item.Number == 11 && group.SourceItemLevel == 7))
            .Select(group => $"{item.Group}/{item.Number}:{group.SourceItemLevel}:{group.Chance}:{group.ItemType}:{group.MinimumLevel}:{group.MaximumLevel}:{group.MoneyAmount}:{group.ExcellentSecondOptionChance}:{group.LuckChance}:{group.SkillChance}:{string.Join(',', group.PossibleItems.Select(reward => $"{reward.Group}/{reward.Number}").Order())}")))
        + string.Join('\n', configuration.Monsters.SelectMany(monster => monster.ItemCraftings.Select(recipe => $"{monster.Number}:{recipe.Number}:{recipe.SimpleCraftingSettings?.SuccessPercent}:{recipe.SimpleCraftingSettings?.Money}:{string.Join(',', recipe.SimpleCraftingSettings?.ResultItems.Select(result => $"{result.ItemDefinition?.Group}/{result.ItemDefinition?.Number}:{result.RandomMinimumLevel}:{result.RandomMaximumLevel}:{result.Durability}") ?? [])}")));
}
