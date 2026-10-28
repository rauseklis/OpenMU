// <copyright file="FlameAreaRadiusUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Restores Scroll of Flame's original 150-world-unit (1.5 tile) target radius.
/// </summary>
[PlugIn]
[Guid("E6207CCB-733B-4AC0-AD2A-936F7A927711")]
public sealed class FlameAreaRadiusUpdatePlugIn : UpdatePlugInBase
{
    private const short FlameSkillNumber = 5;

    /// <inheritdoc />
    public override string Name => "Restore Scroll of Flame radius";

    /// <inheritdoc />
    public override string Description => "Sets Scroll of Flame and its master replacements to the original 1.5-tile target radius.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.FlameAreaRadius;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var skill in gameConfiguration.Skills.Where(IsFlameOrReplacement))
        {
            if (skill.AreaSkillSettings is { } areaSettings)
            {
                areaSettings.TargetAreaDiameter = 3.0f;
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
