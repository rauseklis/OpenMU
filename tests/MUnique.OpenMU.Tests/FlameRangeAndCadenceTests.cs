// <copyright file="FlameRangeAndCadenceTests.cs" company="MUnique">
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
/// Verifies the enlarged and deterministic Scroll of Flame area behavior.
/// </summary>
[TestFixture]
public class FlameRangeAndCadenceTests
{
    private const float ExpectedDiameter = 5.0f;
    private const int ExpectedHitsPerTarget = 2;
    private static readonly TimeSpan ExpectedDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Existing base and master-replacement skills are corrected without replacing unrelated settings.
    /// </summary>
    [Test]
    public async Task UpdateCorrectsExistingFlameAndReplacement()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>();
        var flame = CreateFlameSkill(context, 5);
        var replacement = CreateFlameSkill(context, 378);
        replacement.MasterDefinition = context.CreateNew<MasterSkillDefinition>();
        replacement.MasterDefinition.ReplacedSkill = flame;
        configuration.Skills.Add(flame);
        configuration.Skills.Add(replacement);

        await new FlameRangeAndCadenceUpdatePlugIn().ApplyUpdateAsync(context, configuration);

        foreach (var skill in new[] { flame, replacement })
        {
            Assert.That(skill.AreaSkillSettings!.TargetAreaDiameter, Is.EqualTo(ExpectedDiameter));
            Assert.That(skill.AreaSkillSettings.MinimumNumberOfHitsPerTarget, Is.EqualTo(ExpectedHitsPerTarget));
            Assert.That(skill.AreaSkillSettings.MaximumNumberOfHitsPerTarget, Is.EqualTo(ExpectedHitsPerTarget));
            Assert.That(skill.AreaSkillSettings.DelayBetweenHits, Is.EqualTo(ExpectedDelay));
            Assert.That(skill.AreaSkillSettings.EffectRange, Is.EqualTo(6));
        }
    }

    /// <summary>
    /// A fresh throwaway Season 6 configuration contains the corrected behavior.
    /// </summary>
    [Test]
    public async Task NewSeasonSixDatabaseUsesEnlargedDeterministicFlame()
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
        Assert.That(flame.AreaSkillSettings.MinimumNumberOfHitsPerTarget, Is.EqualTo(ExpectedHitsPerTarget));
        Assert.That(flame.AreaSkillSettings.MaximumNumberOfHitsPerTarget, Is.EqualTo(ExpectedHitsPerTarget));
        Assert.That(flame.AreaSkillSettings.DelayBetweenHits, Is.EqualTo(ExpectedDelay));
    }

    private static Skill CreateFlameSkill(IContext context, short number)
    {
        var skill = context.CreateNew<Skill>();
        skill.Number = number;
        skill.AreaSkillSettings = context.CreateNew<AreaSkillSettings>();
        skill.AreaSkillSettings.TargetAreaDiameter = 3.0f;
        skill.AreaSkillSettings.MinimumNumberOfHitsPerTarget = 1;
        skill.AreaSkillSettings.MaximumNumberOfHitsPerTarget = 2;
        skill.AreaSkillSettings.DelayBetweenHits = ExpectedDelay;
        skill.AreaSkillSettings.EffectRange = 6;
        return skill;
    }
}
