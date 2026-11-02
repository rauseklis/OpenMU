// <copyright file="FlameRadiusOnePointFiveUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>Sets Scroll of Flame and its master replacements to a 1.5-tile target radius.</summary>
[PlugIn]
[Guid("8E2B6D40-71C3-4A5F-B0E9-3D94A1F7C268")]
public sealed class FlameRadiusOnePointFiveUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The target area diameter for a 1.5-tile radius.</summary>
    internal const float FlameDiameter = 3.0f;

    private const short FlameSkillNumber = 5;

    /// <inheritdoc />
    public override string Name => "Scroll of Flame 1.5-tile radius";

    /// <inheritdoc />
    public override string Description => "Sets Scroll of Flame and its master replacements to a 1.5-tile target radius.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.FlameRadiusOnePointFive;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 2, 13, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var skill in gameConfiguration.Skills.Where(IsFlameOrReplacement))
        {
            if (skill.AreaSkillSettings is { } areaSettings)
            {
                areaSettings.TargetAreaDiameter = FlameDiameter;
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
