// <copyright file="ArenaWarriorDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the data of the Arena Warrior monsters (1100-1110) and their spawns on the Arena map.
/// </summary>
[TestFixture]
internal class ArenaWarriorDataTest
{
    private const byte ArenaNumber = 6;
    private const short FirstNumber = 1100;
    private const short LastNumber = 1110;
    private const int SpawnAreas = 15;
    private const int MonstersPerSpawnArea = 8;

    /// <summary>
    /// Tests that a new database contains the monsters and their spawns.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsArenaWarriorsAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);

        AssertArenaWarriors(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update adds the monsters and spawns to an existing database which doesn't have them,
    /// and that applying it twice doesn't change anything.
    /// </summary>
    [Test]
    public async Task UpdateAddsArenaWarriorsAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        var arena = GetArena(gameConfiguration);
        foreach (var spawn in arena.MonsterSpawns.Where(IsArenaWarriorSpawn).ToList())
        {
            arena.MonsterSpawns.Remove(spawn);
        }

        foreach (var monster in gameConfiguration.Monsters.Where(m => m.Number is >= FirstNumber and <= LastNumber).ToList())
        {
            gameConfiguration.Monsters.Remove(monster);
        }

        Assert.That(gameConfiguration.Monsters.Any(m => m.Number is >= FirstNumber and <= LastNumber), Is.False);

        var update = new AddArenaWarriorMonstersUpdatePlugIn();
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);

        AssertArenaWarriors(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update is mandatory, so that it's applied automatically when the server starts.
    /// </summary>
    [Test]
    public void UpdateIsMandatory()
    {
        Assert.That(new AddArenaWarriorMonstersUpdatePlugIn().IsMandatory, Is.True);
    }

    private static bool IsArenaWarriorSpawn(MonsterSpawnArea spawn)
    {
        return spawn.MonsterDefinition?.Number is >= FirstNumber and <= LastNumber;
    }

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    private static GameMapDefinition GetArena(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.Maps.Single(map => map.Number == ArenaNumber);
    }

    private static void AssertArenaWarriors(GameConfiguration gameConfiguration)
    {
        var monsters = gameConfiguration.Monsters.Where(m => m.Number is >= FirstNumber and <= LastNumber).ToList();
        Assert.That(monsters, Has.Count.EqualTo(11));
        Assert.That(monsters.Select(m => m.Number).Distinct().Count(), Is.EqualTo(11));

        // Exact values of the source file, spot-checked on the weakest and the strongest monster.
        var scale = monsters.Single(m => m.Number == 1100);
        Assert.That(scale[Stats.MaximumHealth], Is.EqualTo(19000));
        Assert.That(scale[Stats.MinimumPhysBaseDmg], Is.EqualTo(305));
        Assert.That(scale[Stats.MaximumPhysBaseDmg], Is.EqualTo(350));
        Assert.That(scale[Stats.DefenseBase], Is.EqualTo(230));
        Assert.That(scale.AttackSkill, Is.Null);
        var dragonKnight = monsters.Single(m => m.Number == 1110);
        Assert.That(dragonKnight[Stats.MaximumHealth], Is.EqualTo(237000));
        Assert.That(dragonKnight[Stats.MaximumPhysBaseDmg], Is.EqualTo(2317));
        Assert.That(dragonKnight[Stats.LightningResistance], Is.EqualTo(150f / 255).Within(0.0001));
        Assert.That(dragonKnight.AttackDelay, Is.EqualTo(TimeSpan.FromMilliseconds(2500)));
        Assert.That(dragonKnight.RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(10)));

        // Skill numbers: Triple Shot 24, Meteorite 2, Lightning 3.
        Assert.That(monsters.Single(m => m.Number == 1106).AttackSkill?.Number, Is.EqualTo((short)24));
        Assert.That(monsters.Single(m => m.Number == 1107).AttackSkill?.Number, Is.EqualTo((short)2));
        Assert.That(monsters.Single(m => m.Number == 1108).AttackSkill?.Number, Is.EqualTo((short)3));

        var spawns = GetArena(gameConfiguration).MonsterSpawns.Where(IsArenaWarriorSpawn).ToList();
        Assert.That(spawns, Has.Count.EqualTo(SpawnAreas));
        Assert.That(spawns.All(s => s.Quantity == MonstersPerSpawnArea && s.SpawnTrigger == SpawnTrigger.Automatic), Is.True);
        Assert.That(spawns.Select(s => s.MonsterDefinition!.Number).Distinct().Count(), Is.EqualTo(11));
    }
}
