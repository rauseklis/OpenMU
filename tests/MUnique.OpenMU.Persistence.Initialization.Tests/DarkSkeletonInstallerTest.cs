// <copyright file="DarkSkeletonInstallerTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the installation of the dark skeleton update at the start of the server.
/// </summary>
[TestFixture]
internal class DarkSkeletonInstallerTest
{
    private const byte DeviasNumber = 2;

    /// <summary>
    /// Tests that the update of a database which doesn't have the monsters yet is installed once, is recorded as
    /// installed, and doesn't touch the other spawns of Devias.
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
            var devias = configuration.Maps.Single(m => m.Number == DeviasNumber);
            foreach (var spawn in devias.MonsterSpawns.Where(IsDarkSkeletonSpawn).ToList())
            {
                devias.MonsterSpawns.Remove(spawn);
                await context.DeleteAsync(spawn).ConfigureAwait(false);
            }

            foreach (var monster in configuration.Monsters.Where(IsDarkSkeleton).ToList())
            {
                foreach (var attribute in monster.Attributes.ToList())
                {
                    await context.DeleteAsync(attribute).ConfigureAwait(false);
                }

                configuration.Monsters.Remove(monster);
                await context.DeleteAsync(monster).ConfigureAwait(false);
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddDarkSkeletonMonsters).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            unrelatedSpawns = devias.MonsterSpawns.Count;
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await DarkSkeletonMonstersInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True, "first start");
        Assert.That(await DarkSkeletonMonstersInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "second start");

        using var reloaded = provider.CreateNewContext();
        var reloadedConfiguration = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        Assert.That(reloadedConfiguration.Monsters.Count(IsDarkSkeleton), Is.EqualTo(2));
        var reloadedDevias = reloadedConfiguration.Maps.Single(m => m.Number == DeviasNumber);
        var skeletonSpawns = reloadedDevias.MonsterSpawns.Where(IsDarkSkeletonSpawn).ToList();
        Assert.That(skeletonSpawns, Has.Count.EqualTo(2));
        Assert.That(skeletonSpawns.Sum(s => s.Quantity), Is.EqualTo(2));
        Assert.That(reloadedDevias.MonsterSpawns.Count - skeletonSpawns.Count, Is.EqualTo(unrelatedSpawns));
        var installed = (await reloaded.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddDarkSkeletonMonsters).ToList();
        Assert.That(installed, Has.Count.EqualTo(1));
        Assert.That(installed[0].InstalledAt, Is.Not.Null);
        var state = (await reloaded.GetAsync<ConfigurationUpdateState>().ConfigureAwait(false)).Single();
        Assert.That(state.CurrentInstalledVersion, Is.GreaterThanOrEqualTo((int)UpdateVersion.AddDarkSkeletonMonsters));
    }

    /// <summary>
    /// Tests that nothing is installed again when the update is installed already, e.g. in a new database.
    /// </summary>
    [Test]
    public async Task DoesNothingWhenTheUpdateIsInstalledAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);

        Assert.That(await DarkSkeletonMonstersInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
    }

    private static bool IsDarkSkeleton(MonsterDefinition monster) => monster.Number is 1111 or 1112;

    private static bool IsDarkSkeletonSpawn(MonsterSpawnArea spawn) => spawn.MonsterDefinition is { } monster && IsDarkSkeleton(monster);
}
