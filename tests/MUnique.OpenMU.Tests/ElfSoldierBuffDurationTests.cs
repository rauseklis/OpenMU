// <copyright file="ElfSoldierBuffDurationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Verifies the 2 hours 30 minutes duration of the Elf Soldier buff.
/// </summary>
[TestFixture]
public class ElfSoldierBuffDurationTests
{
    private const float ExpectedSeconds = 9000f;

    /// <summary>
    /// A new Season 6 database starts with the 2h 30min duration.
    /// </summary>
    [Test]
    public async Task NewDatabaseUsesTwoHoursThirtyMinutes()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        var effect = configuration.MagicEffects.Single(e => e.Number == 3);

        Assert.That(effect.Duration!.ConstantValue.Value, Is.EqualTo(ExpectedSeconds));
    }

    /// <summary>
    /// An existing database receives the duration exactly once, and later edits are kept.
    /// </summary>
    [Test]
    public async Task StartupInstallerUpdatesAnExistingDatabaseOnce()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var effect = configuration.MagicEffects.Single(e => e.Number == 3);
        effect.Duration!.ConstantValue.Value = 3600f;
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.ElfSoldierBuffDuration)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await ElfSoldierBuffDurationInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        Assert.That(effect.Duration.ConstantValue.Value, Is.EqualTo(ExpectedSeconds));

        effect.Duration.ConstantValue.Value = 1200f;
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await ElfSoldierBuffDurationInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
        Assert.That(effect.Duration.ConstantValue.Value, Is.EqualTo(1200f));
    }
}
