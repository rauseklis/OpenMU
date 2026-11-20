// <copyright file="ArenaWarriorInstallerTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the installation of the Arena Warrior update at the start of the server.
/// </summary>
[TestFixture]
internal class ArenaWarriorInstallerTest
{
    private const byte ArenaNumber = 6;

    /// <summary>
    /// Tests that the update of a database which doesn't have the monsters yet is installed once, is recorded as
    /// installed, and doesn't touch the other spawns of the Arena.
    /// </summary>
    [Test]
    public async Task InstallsTheUpdateOnlyOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        int unrelatedSpawns;
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var arena = configuration.Maps.Single(m => m.Number == ArenaNumber);
            foreach (var spawn in arena.MonsterSpawns.Where(IsArenaWarriorSpawn).ToList())
            {
                arena.MonsterSpawns.Remove(spawn);
                await context.DeleteAsync(spawn).ConfigureAwait(false);
            }

            foreach (var monster in configuration.Monsters.Where(m => m.Number is >= 1100 and <= 1110).ToList())
            {
                foreach (var attribute in monster.Attributes.ToList())
                {
                    await context.DeleteAsync(attribute).ConfigureAwait(false);
                }

                configuration.Monsters.Remove(monster);
                await context.DeleteAsync(monster).ConfigureAwait(false);
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddArenaWarriorMonsters).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            unrelatedSpawns = arena.MonsterSpawns.Count;
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await ArenaWarriorMonstersInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True, "first start");
        Assert.That(await ArenaWarriorMonstersInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "second start");

        using var reloaded = provider.CreateNewContext();
        var reloadedConfiguration = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        Assert.That(reloadedConfiguration.Monsters.Count(m => m.Number is >= 1100 and <= 1110), Is.EqualTo(11));
        var reloadedArena = reloadedConfiguration.Maps.Single(m => m.Number == ArenaNumber);
        var warriorSpawns = reloadedArena.MonsterSpawns.Where(IsArenaWarriorSpawn).ToList();
        Assert.That(warriorSpawns, Has.Count.EqualTo(15));
        Assert.That(warriorSpawns.Sum(s => s.Quantity), Is.EqualTo(120));
        Assert.That(reloadedArena.MonsterSpawns.Count - warriorSpawns.Count, Is.EqualTo(unrelatedSpawns));
        var installed = (await reloaded.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddArenaWarriorMonsters).ToList();
        Assert.That(installed, Has.Count.EqualTo(1));
        Assert.That(installed[0].InstalledAt, Is.Not.Null);
        var state = (await reloaded.GetAsync<ConfigurationUpdateState>().ConfigureAwait(false)).Single();
        Assert.That(state.CurrentInstalledVersion, Is.GreaterThanOrEqualTo((int)UpdateVersion.AddArenaWarriorMonsters));
    }

    /// <summary>
    /// Tests that nothing is installed again when the update is installed already, e.g. in a new database.
    /// </summary>
    [Test]
    public async Task DoesNothingWhenTheUpdateIsInstalledAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);

        Assert.That(await ArenaWarriorMonstersInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
    }

    private static bool IsArenaWarriorSpawn(MonsterSpawnArea spawn)
    {
        return spawn.MonsterDefinition?.Number is >= 1100 and <= 1110;
    }
}
