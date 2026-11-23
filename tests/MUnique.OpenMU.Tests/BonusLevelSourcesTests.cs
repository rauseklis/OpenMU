// <copyright file="BonusLevelSourcesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Pins the monster-level bands of the small bonuses.
/// </summary>
[TestFixture]
public class BonusLevelSourcesTests
{
    private static readonly Dictionary<byte, (int Min, int Max, double Chance)> Bands = new()
    {
        [1] = (72, 99, .0005),
        [2] = (60, 74, .0015),
        [3] = (50, 60, .003),
        [5] = (1, 44, .01),
        [6] = (45, 59, .0035),
        [7] = (20, 74, .0005),
    };

    private GameConfiguration _configuration = null!;

    /// <summary>
    /// Creates a fresh Season 6 configuration once.
    /// </summary>
    /// <returns>The task.</returns>
    [OneTimeSetUp]
    public async Task InitializeAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        this._configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
    }

    /// <summary>
    /// Every source group has the band's level range and chance, and its monster lies inside the band.
    /// </summary>
    [Test]
    public void EverySourceFollowsItsBand()
    {
        var groups = this.Sources().ToArray();
        Assert.That(groups.Select(group => group.ItemLevel!.Value).Distinct(), Is.EquivalentTo(Bands.Keys));
        foreach (var group in groups)
        {
            var (min, max, chance) = Bands[group.ItemLevel!.Value];
            Assert.That(group.Chance, Is.EqualTo(chance).Within(1e-12), group.Description);
            Assert.That(group.MinimumMonsterLevel, Is.EqualTo(min), group.Description);
            Assert.That(group.MaximumMonsterLevel, Is.EqualTo(max), group.Description);
            Assert.That(group.Monster![Stats.Level], Is.InRange(min, max), group.Description);
            Assert.That(group.Monster.ObjectKind, Is.EqualTo(NpcObjectKind.Monster));
            Assert.That(group.Monster.NumberOfMaximumItemDrops, Is.GreaterThan(0));
        }
    }

    /// <summary>
    /// Each monster gets exactly one group per band it lies in (no duplicates), and every spawning monster of a band is covered.
    /// </summary>
    [Test]
    public void EveryEligibleMonsterGetsExactlyOneGroupPerBand()
    {
        var groups = this.Sources().ToArray();
        Assert.That(groups.Select(group => (group.Monster, group.ItemLevel)).Distinct().Count(), Is.EqualTo(groups.Length));
        var eligible = this._configuration.Maps.Where(map => map.BattleZone is null).SelectMany(map => map.MonsterSpawns)
            .Where(spawn => spawn.Quantity > 0 && spawn.SpawnTrigger is SpawnTrigger.Automatic or SpawnTrigger.AutomaticDuringEvent or SpawnTrigger.OnceAtEventStart or SpawnTrigger.AutomaticDuringWave
                && spawn.MonsterDefinition is { ObjectKind: NpcObjectKind.Monster, NumberOfMaximumItemDrops: > 0 } monster
                && !monster.DropItemGroups.Any(group => group.ItemType != SpecialItemType.SmallBonus && group.Chance >= 1))
            .Select(spawn => spawn.MonsterDefinition!).Distinct().ToArray();
        foreach (var (level, band) in Bands)
        {
            var expected = eligible.Where(monster => monster[Stats.Level] >= band.Min && monster[Stats.Level] <= band.Max).ToHashSet();
            var actual = groups.Where(group => group.ItemLevel == level).Select(group => group.Monster!).ToHashSet();
            Assert.That(actual, Is.EquivalentTo(expected), $"bonus level {level}");
        }
    }

    /// <summary>
    /// A group is attached to every map where its monster spawns, and only there.
    /// </summary>
    [Test]
    public void GroupsAreAttachedToTheMapsOfTheirMonster()
    {
        foreach (var map in this._configuration.Maps)
        {
            foreach (var group in map.DropItemGroups.Where(IsSource))
            {
                Assert.That(map.MonsterSpawns.Any(spawn => spawn.MonsterDefinition == group.Monster), Is.True, $"{map.Number}: {group.Description}");
            }
        }
    }

    /// <summary>
    /// Kalima, Blood Castle and Devil Square monsters now drop bonuses.
    /// </summary>
    [Test]
    public void KalimaBloodCastleAndDevilSquareDropBonuses()
    {
        var withSources = this._configuration.Maps.Where(map => map.DropItemGroups.Any(IsSource)).Select(map => map.Name.Value ?? string.Empty).ToArray();
        TestContext.WriteLine($"Maps with level-based bonuses ({withSources.Length}): {string.Join(", ", withSources)}");
        Assert.That(withSources.Any(name => name.StartsWith("Kalima", StringComparison.Ordinal)), Is.True, "Kalima");
        Assert.That(withSources.Any(name => name.StartsWith("Blood Castle", StringComparison.Ordinal)), Is.True, "Blood Castle");
        Assert.That(withSources.Any(name => name.StartsWith("Devil Square", StringComparison.Ordinal)), Is.True, "Devil Square");
    }

    /// <summary>
    /// Battle zones, boss-wave starts and manually spawned event monsters are not sources.
    /// </summary>
    [Test]
    public void BattleZonesAndBossWaveOrManualSpawnsAreExcluded()
    {
        foreach (var map in this._configuration.Maps)
        {
            var spawnsOnlyHere = map.DropItemGroups.Where(IsSource).Where(group => !map.MonsterSpawns.Any(spawn => spawn.MonsterDefinition == group.Monster
                && spawn.Quantity > 0 && spawn.SpawnTrigger is SpawnTrigger.Automatic or SpawnTrigger.AutomaticDuringEvent or SpawnTrigger.OnceAtEventStart or SpawnTrigger.AutomaticDuringWave)).ToArray();
            Assert.That(spawnsOnlyHere, Is.Empty, $"{map.Number} {map.Name.Value}");
            if (map.BattleZone is not null)
            {
                Assert.That(map.DropItemGroups.Any(IsSource), Is.False, $"battle zone {map.Number}");
            }
        }
    }

    /// <summary>
    /// A level 55 monster can drop Gold, Heart of Love and Box of Heaven (overlapping bands); a level 60 monster Heart and Firecracker.
    /// </summary>
    [TestCase(55, new byte[] { 3, 6, 7 })]
    [TestCase(60, new byte[] { 2, 3, 7 })]
    [TestCase(44, new byte[] { 5, 7 })]
    [TestCase(45, new byte[] { 6, 7 })]
    [TestCase(75, new byte[] { 1 })]
    public void BandsOverlapAsApproved(int level, byte[] expected)
    {
        var levels = Bands.Where(band => level >= band.Value.Min && level <= band.Value.Max).Select(band => band.Key).Order().ToArray();
        Assert.That(levels, Is.EqualTo(expected));
    }

    /// <summary>
    /// Re-applying the sources is idempotent and keeps mini-game reward groups.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ReapplyingIsIdempotentAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var before = Fingerprint(configuration);
        var rewards = configuration.MiniGameDefinitions.SelectMany(game => game.Rewards).Select(reward => reward.ItemReward).OfType<DropItemGroup>().ToArray();

        new BonusLevelSources(context, configuration).Initialize();

        Assert.That(Fingerprint(configuration), Is.EqualTo(before));
        Assert.That(rewards.All(reward => configuration.DropItemGroups.Contains(reward)), Is.True);
    }

    /// <summary>
    /// An existing database gets the level-based sources exactly once.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task StartupInstallerRebuildsAnExistingDatabaseOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        foreach (var group in configuration.DropItemGroups.Where(IsSource).ToArray())
        {
            group.Chance = .5;
        }

        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.BonusLevelSources).ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await BonusLevelSourcesInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(configuration.DropItemGroups.Where(IsSource).All(group => group.Chance < .5), Is.True);
        Assert.That(await BonusLevelSourcesInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
    }

    /// <summary>
    /// Prints the number of dropping monsters per bonus and per event map family.
    /// </summary>
    [Test]
    public void ReportMonsterCounts()
    {
        foreach (var level in Bands.Keys.Order())
        {
            var groups = this.Sources().Where(group => group.ItemLevel == level).ToArray();
            TestContext.WriteLine($"bonus {level}: {groups.Length} monsters");
        }

        foreach (var family in new[] { "Kalima", "Blood Castle", "Devil Square" })
        {
            var monsters = this._configuration.Maps.Where(map => (map.Name.Value ?? string.Empty).StartsWith(family, StringComparison.Ordinal))
                .SelectMany(map => map.DropItemGroups.Where(IsSource)).Select(group => group.Monster!.Number).Distinct().Count();
            TestContext.WriteLine($"{family}: {monsters} distinct monsters drop bonuses");
        }
    }

    private static bool IsSource(DropItemGroup group) => group.ItemType == SpecialItemType.SmallBonus
        && group.PossibleItems.Count == 1 && group.PossibleItems.Single() is { Group: 14, Number: 11 }
        && group.Monster is not null;

    private static string Fingerprint(GameConfiguration configuration) => string.Join('\n', configuration.Maps.OrderBy(map => map.Number)
        .SelectMany(map => map.DropItemGroups.Where(IsSource).Select(group => $"{map.Number}:{group.Monster!.Number}:{group.ItemLevel}:{group.Chance:R}:{group.MinimumMonsterLevel}:{group.MaximumMonsterLevel}")).Order());

    private IEnumerable<DropItemGroup> Sources() => this._configuration.DropItemGroups.Where(IsSource);
}
