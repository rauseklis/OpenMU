// <copyright file="DarkSkeletonDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the data of the dark skeletons (Dark Skeleton 1111, Dark Reaper 1112) and their Devias test spawns.
/// </summary>
[TestFixture]
internal class DarkSkeletonDataTest
{
    private const byte DeviasNumber = 2;
    private const short DarkSkeleton = 1111;
    private const short DarkReaper = 1112;
    private const short Hellfire = 10;
    private const short EvilSpirit = 9;

    /// <summary>
    /// Tests that a new database contains the monsters and their spawns.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsDarkSkeletonsAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);

        AssertDarkSkeletons(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update adds the monsters and spawns to an existing database which doesn't have them,
    /// and that applying it twice doesn't change anything.
    /// </summary>
    [Test]
    public async Task UpdateAddsDarkSkeletonsAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        var devias = GetDevias(gameConfiguration);
        foreach (var spawn in devias.MonsterSpawns.Where(IsDarkSkeletonSpawn).ToList())
        {
            devias.MonsterSpawns.Remove(spawn);
        }

        foreach (var monster in gameConfiguration.Monsters.Where(IsDarkSkeleton).ToList())
        {
            gameConfiguration.Monsters.Remove(monster);
        }

        Assert.That(gameConfiguration.Monsters.Any(IsDarkSkeleton), Is.False);

        var update = new AddDarkSkeletonMonstersUpdatePlugIn();
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);

        AssertDarkSkeletons(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update is mandatory, so that it's applied automatically when the server starts.
    /// </summary>
    [Test]
    public void UpdateIsMandatory()
    {
        Assert.That(new AddDarkSkeletonMonstersUpdatePlugIn().IsMandatory, Is.True);
    }

    private static bool IsDarkSkeleton(MonsterDefinition monster) => monster.Number is DarkSkeleton or DarkReaper;

    private static bool IsDarkSkeletonSpawn(MonsterSpawnArea spawn) => spawn.MonsterDefinition is { } monster && IsDarkSkeleton(monster);

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    private static GameMapDefinition GetDevias(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.Maps.Single(map => map.Number == DeviasNumber);
    }

    private static void AssertDarkSkeletons(GameConfiguration gameConfiguration)
    {
        var monsters = gameConfiguration.Monsters.Where(IsDarkSkeleton).ToList();
        Assert.That(monsters, Has.Count.EqualTo(2));

        var skeleton = monsters.Single(m => m.Number == DarkSkeleton);
        var reaper = monsters.Single(m => m.Number == DarkReaper);
        Assert.That(skeleton.Designation.ToString(), Is.EqualTo("Dark Skeleton"));
        Assert.That(reaper.Designation.ToString(), Is.EqualTo("Dark Reaper"));

        // The owner's values: 10 000 health, everything else 100.
        foreach (var monster in monsters)
        {
            Assert.That(monster[Stats.Level], Is.EqualTo(100));
            Assert.That(monster[Stats.MaximumHealth], Is.EqualTo(10000));
            Assert.That(monster[Stats.MinimumPhysBaseDmg], Is.EqualTo(100));
            Assert.That(monster[Stats.MaximumPhysBaseDmg], Is.EqualTo(100));
            Assert.That(monster[Stats.DefenseBase], Is.EqualTo(100));
            Assert.That(monster[Stats.AttackRatePvm], Is.EqualTo(100));
            Assert.That(monster[Stats.DefenseRatePvm], Is.EqualTo(100));
        }

        Assert.That(skeleton.AttackSkill?.Number, Is.EqualTo(EvilSpirit));
        Assert.That(reaper.AttackSkill?.Number, Is.EqualTo(Hellfire));
        Assert.That(skeleton.RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(10)));
        Assert.That(reaper.RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(20)));

        var spawns = GetDevias(gameConfiguration).MonsterSpawns.Where(IsDarkSkeletonSpawn).ToList();
        Assert.That(spawns, Has.Count.EqualTo(2));
        Assert.That(spawns.All(s => s.Quantity == 1 && s.SpawnTrigger == SpawnTrigger.Automatic), Is.True);
        var skeletonSpawn = spawns.Single(s => s.MonsterDefinition!.Number == DarkSkeleton);
        Assert.That((skeletonSpawn.X1, skeletonSpawn.X2, skeletonSpawn.Y1, skeletonSpawn.Y2), Is.EqualTo(((byte)221, (byte)221, (byte)81, (byte)81)));
        var reaperSpawn = spawns.Single(s => s.MonsterDefinition!.Number == DarkReaper);
        Assert.That((reaperSpawn.X1, reaperSpawn.X2, reaperSpawn.Y1, reaperSpawn.Y2), Is.EqualTo(((byte)222, (byte)222, (byte)85, (byte)85)));
    }
}
