// <copyright file="CrywolfInstallerTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the installation of the crywolf data update at the start of the server.
/// </summary>
[TestFixture]
internal class CrywolfInstallerTest
{
    private const short CrywolfNumber = 34;

    /// <summary>
    /// Tests that the update of a database which doesn't have it yet is installed once, is recorded as installed,
    /// and doesn't touch unrelated configuration.
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
            var map = configuration.Maps.Single(m => m.Number == CrywolfNumber);
            foreach (var spawn in map.MonsterSpawns.Where(s => s.SpawnTrigger == SpawnTrigger.OnceAtWaveStart).ToList())
            {
                map.MonsterSpawns.Remove(spawn);
                await context.DeleteAsync(spawn).ConfigureAwait(false);
            }

            foreach (var variant in map.TerrainVariants.ToList())
            {
                map.TerrainVariants.Remove(variant);
                await context.DeleteAsync(variant).ConfigureAwait(false);
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddCrywolfEvent).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            unrelatedSpawns = map.MonsterSpawns.Count(s => s.SpawnTrigger != SpawnTrigger.OnceAtWaveStart);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await CrywolfEventInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True, "first start");
        Assert.That(await CrywolfEventInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "second start");

        using var reloaded = provider.CreateNewContext();
        var reloadedConfiguration = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var reloadedMap = reloadedConfiguration.Maps.Single(m => m.Number == CrywolfNumber);
        Assert.That(reloadedMap.MonsterSpawns.Count(s => s.SpawnTrigger == SpawnTrigger.OnceAtWaveStart), Is.EqualTo(76));
        Assert.That(reloadedMap.TerrainVariants, Has.Count.EqualTo(2));
        Assert.That(reloadedMap.MonsterSpawns.Count(s => s.SpawnTrigger != SpawnTrigger.OnceAtWaveStart), Is.EqualTo(unrelatedSpawns));
        var installed = (await reloaded.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddCrywolfEvent).ToList();
        Assert.That(installed, Has.Count.EqualTo(1));
        Assert.That(installed[0].InstalledAt, Is.Not.Null);
        var state = (await reloaded.GetAsync<ConfigurationUpdateState>().ConfigureAwait(false)).Single();
        Assert.That(state.CurrentInstalledVersion, Is.GreaterThanOrEqualTo((int)UpdateVersion.AddCrywolfEvent));
    }

    /// <summary>
    /// Tests that nothing is installed again when the update is installed already, e.g. through the admin panel.
    /// </summary>
    [Test]
    public async Task DoesNothingWhenTheUpdateIsInstalledAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);

        Assert.That(await CrywolfEventInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
    }
}
