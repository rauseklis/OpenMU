// <copyright file="FlameRangeAndCadenceUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Enlarges Scroll of Flame's target area and makes both attack rounds deterministic.
/// </summary>
[PlugIn]
[Guid("A28DC84A-E3E5-4B11-8D73-770C37CE48DB")]
public sealed class FlameRangeAndCadenceUpdatePlugIn : UpdatePlugInBase
{
    private const short FlameSkillNumber = 5;

    /// <inheritdoc />
    public override string Name => "Enlarge Scroll of Flame area and guarantee both hits";

    /// <inheritdoc />
    public override string Description => "Sets Scroll of Flame and its master replacements to a 2.5-tile radius with two guaranteed hits 500 ms apart.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.FlameRangeAndCadence;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var skill in gameConfiguration.Skills.Where(IsFlameOrReplacement))
        {
            if (skill.AreaSkillSettings is { } areaSettings)
            {
                areaSettings.TargetAreaDiameter = 5.0f;
                areaSettings.MinimumNumberOfHitsPerTarget = 2;
                areaSettings.MaximumNumberOfHitsPerTarget = 2;
                areaSettings.DelayBetweenHits = TimeSpan.FromMilliseconds(500);
            }
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsFlameOrReplacement(Skill skill)
    {
        for (Skill? current = skill; current is not null; current = current.MasterDefinition?.ReplacedSkill)
        {
            if (current.Number == FlameSkillNumber)
            {
                return true;
            }
        }

        return false;
    }
}
