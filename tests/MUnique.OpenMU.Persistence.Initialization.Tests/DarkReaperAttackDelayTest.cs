// <copyright file="DarkReaperAttackDelayTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests that the Dark Reaper (1112) hits once every 5 seconds and that nothing else changes.
/// </summary>
[TestFixture]
internal class DarkReaperAttackDelayTest
{
    private const short DarkSkeleton = 1111;
    private const short DarkReaper = 1112;
    private const short DarkMage = 1113;

    /// <summary>
    /// Tests that a new database has the Dark Reaper with one hit every 5 seconds.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasTheNewDelayAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var configuration = await CreateConfigurationAsync(provider).ConfigureAwait(false);

        Assert.That(configuration.Monsters.Single(m => m.Number == DarkReaper).AttackDelay, Is.EqualTo(TimeSpan.FromSeconds(5)));
    }

    /// <summary>
    /// Tests that the update changes only the attack delay of the Dark Reaper on a database with the old 2 seconds
    /// (like the live server), also when applied twice.
    /// </summary>
    [Test]
    public async Task UpdateChangesOnlyTheReaperDelayAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var configuration = await CreateConfigurationAsync(provider).ConfigureAwait(false);
        var reaper = configuration.Monsters.Single(m => m.Number == DarkReaper);
        reaper.AttackDelay = TimeSpan.FromSeconds(2);
        var healthBefore = reaper[Stats.MaximumHealth];
        var rangeBefore = reaper.AttackRange;

        var update = new ChangeDarkReaperAttackDelayUpdatePlugIn();
        await update.ApplyUpdateAsync(provider.CreateNewContext(), configuration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(provider.CreateNewContext(), configuration).ConfigureAwait(false);

        Assert.That(reaper.AttackDelay, Is.EqualTo(TimeSpan.FromSeconds(5)));
        Assert.That((reaper[Stats.MaximumHealth], reaper.AttackRange), Is.EqualTo((healthBefore, rangeBefore)));
        Assert.That(configuration.Monsters.Single(m => m.Number == DarkSkeleton).AttackDelay, Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(configuration.Monsters.Single(m => m.Number == DarkMage).AttackDelay, Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(update.IsMandatory, Is.True);
    }

    /// <summary>
    /// Tests that the installer changes the delay once, and does nothing on a new database.
    /// </summary>
    [Test]
    public async Task InstallerRunsOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        Assert.That(await DarkReaperAttackDelayInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "new database");

        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            configuration.Monsters.Single(m => m.Number == DarkReaper).AttackDelay = TimeSpan.FromSeconds(2);
            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.ChangeDarkReaperAttackDelay).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await DarkReaperAttackDelayInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True, "first start");
        Assert.That(await DarkReaperAttackDelayInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "second start");

        using var reloaded = provider.CreateNewContext();
        var reloadedConfiguration = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        Assert.That(reloadedConfiguration.Monsters.Single(m => m.Number == DarkReaper).AttackDelay, Is.EqualTo(TimeSpan.FromSeconds(5)));
    }

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider provider)
    {
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
