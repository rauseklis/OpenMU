// <copyright file="FlameAreaRadiusTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Verifies that Scroll of Flame covers the original client's 150-world-unit (1.5 tile) radius.
/// </summary>
[TestFixture]
public class FlameAreaRadiusTests
{
    private const float ExpectedDiameter = 3.0f;
    private const int ExpectedMinimumHitsPerTarget = 1;

    /// <summary>
    /// Existing configurations are corrected without replacing the skill or its other area settings.
    /// </summary>
    [Test]
    public async Task UpdateCorrectsExistingFlameRadius()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>();
        var flame = context.CreateNew<Skill>();
        flame.Number = 5; // Flame
        flame.AreaSkillSettings = context.CreateNew<AreaSkillSettings>();
        flame.AreaSkillSettings.TargetAreaDiameter = 2.0f;
        flame.AreaSkillSettings.MinimumNumberOfHitsPerTarget = 0;
        flame.AreaSkillSettings.DelayBetweenHits = TimeSpan.FromMilliseconds(500);
        configuration.Skills.Add(flame);

        await new FlameAreaRadiusUpdatePlugIn().ApplyUpdateAsync(context, configuration);

        Assert.That(flame.AreaSkillSettings.TargetAreaDiameter, Is.EqualTo(ExpectedDiameter));
        Assert.That(flame.AreaSkillSettings.MinimumNumberOfHitsPerTarget, Is.Zero);
        Assert.That(flame.AreaSkillSettings.DelayBetweenHits, Is.EqualTo(TimeSpan.FromMilliseconds(500)));
    }

    /// <summary>
    /// Existing configurations gain one guaranteed attempt without changing their radius or cadence.
    /// </summary>
    [Test]
    public async Task MinimumHitUpdateCorrectsExistingFlameSkill()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>();
        var flame = context.CreateNew<Skill>();
        flame.Number = 5; // Flame
        flame.AreaSkillSettings = context.CreateNew<AreaSkillSettings>();
        flame.AreaSkillSettings.TargetAreaDiameter = ExpectedDiameter;
        flame.AreaSkillSettings.MinimumNumberOfHitsPerTarget = 0;
        flame.AreaSkillSettings.DelayBetweenHits = TimeSpan.FromMilliseconds(500);
        configuration.Skills.Add(flame);

        await new FlameMinimumHitUpdatePlugIn().ApplyUpdateAsync(context, configuration);

        Assert.That(flame.AreaSkillSettings.TargetAreaDiameter, Is.EqualTo(ExpectedDiameter));
        Assert.That(flame.AreaSkillSettings.MinimumNumberOfHitsPerTarget, Is.EqualTo(ExpectedMinimumHitsPerTarget));
        Assert.That(flame.AreaSkillSettings.DelayBetweenHits, Is.EqualTo(TimeSpan.FromMilliseconds(500)));
    }

    /// <summary>
    /// A freshly initialized Season 6 database has the corrected radius without requiring an update.
    /// </summary>
    [Test]
    public async Task NewSeasonSixDatabaseUsesOriginalClientRadius()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        var flame = configuration.Skills.Single(skill => skill.Number == 5);

        Assert.That(flame.AreaSkillSettings, Is.Not.Null);
        Assert.That(flame.AreaSkillSettings!.TargetAreaDiameter, Is.EqualTo(ExpectedDiameter));
        Assert.That(flame.AreaSkillSettings.MinimumNumberOfHitsPerTarget, Is.EqualTo(ExpectedMinimumHitsPerTarget));
    }
}
