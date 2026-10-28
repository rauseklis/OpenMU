// <copyright file="FlameMinimumHitUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Guarantees Scroll of Flame one hit attempt per selected target.
/// </summary>
[PlugIn]
[Guid("C60BCE26-D911-4DBB-8BE6-2A5087366081")]
public sealed class FlameMinimumHitUpdatePlugIn : UpdatePlugInBase
{
    private const short FlameSkillNumber = 5;

    /// <inheritdoc />
    public override string Name => "Restore Scroll of Flame minimum hit attempt";

    /// <inheritdoc />
    public override string Description => "Guarantees one hit attempt for Scroll of Flame and its master replacements before distance falloff applies.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.FlameMinimumHit;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 28, 17, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var skill in gameConfiguration.Skills.Where(IsFlameOrReplacement))
        {
            if (skill.AreaSkillSettings is { } areaSettings)
            {
                areaSettings.MinimumNumberOfHitsPerTarget = 1;
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
