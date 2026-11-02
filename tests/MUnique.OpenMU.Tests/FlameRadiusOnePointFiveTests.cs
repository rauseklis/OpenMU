// <copyright file="FlameRadiusOnePointFiveTests.cs" company="MUnique">
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
/// Verifies the 1.5-tile Scroll of Flame radius.
/// </summary>
[TestFixture]
public class FlameRadiusOnePointFiveTests
{
    private const float ExpectedDiameter = 3.0f;

    /// <summary>
    /// A new Season 6 database starts with the 1.5-tile radius.
    /// </summary>
    [Test]
    public async Task NewDatabaseUsesOnePointFiveTiles()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        var flame = configuration.Skills.Single(skill => skill.Number == 5);

        Assert.That(flame.AreaSkillSettings!.TargetAreaDiameter, Is.EqualTo(ExpectedDiameter));
    }

    /// <summary>
    /// An existing database receives the radius exactly once, and later edits are kept.
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
        var flame = configuration.Skills.Single(skill => skill.Number == 5);
        flame.AreaSkillSettings!.TargetAreaDiameter = 2.0f;
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.FlameRadiusOnePointFive)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await FlameRadiusOnePointFiveInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        Assert.That(flame.AreaSkillSettings.TargetAreaDiameter, Is.EqualTo(ExpectedDiameter));

        flame.AreaSkillSettings.TargetAreaDiameter = 4.0f;
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await FlameRadiusOnePointFiveInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
        Assert.That(flame.AreaSkillSettings.TargetAreaDiameter, Is.EqualTo(4.0f));
    }
}
