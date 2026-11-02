// <copyright file="ReportedFixesUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;
using MUnique.OpenMU.PlugIns;

/// <summary>Switches on the single-word stat commands, narrows Scroll of Flame to a 1-tile radius and limits Loch's Feather to Dark Phoenix.</summary>
[PlugIn]
[Guid("5C0E7B1D-3A94-4F62-9D48-1B7A62C0E3F5")]
public sealed class ReportedFixesUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The target area diameter for a 1-tile radius.</summary>
    internal const float FlameDiameter = 2.0f;

    private const short FlameSkillNumber = 5;
    private const short DarkPhoenixNumber = 77;

    private static readonly Guid[] StatCommandTypeIds =
    [
        typeof(AddStrengthStatChatCommandPlugIn).GUID,
        typeof(AddAgilityStatChatCommandPlugIn).GUID,
        typeof(AddVitalityStatChatCommandPlugIn).GUID,
        typeof(AddEnergyStatChatCommandPlugIn).GUID,
        typeof(AddCommandStatChatCommandPlugIn).GUID,
    ];

    /// <inheritdoc />
    public override string Name => "Stat commands, Flame radius and Loch's Feather source";

    /// <inheritdoc />
    public override string Description => "Enables /addstr, /addagi, /addvit, /addene and /addcmd, sets Scroll of Flame to a 1-tile radius and lets Loch's Feather drop on Icarus only from Dark Phoenix.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ReportedFixes;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var configuration in gameConfiguration.PlugInConfigurations.Where(c => StatCommandTypeIds.Contains(c.TypeId)))
        {
            configuration.IsActive = true;
        }

        foreach (var skill in gameConfiguration.Skills.Where(IsFlameOrReplacement))
        {
            if (skill.AreaSkillSettings is { } areaSettings)
            {
                areaSettings.TargetAreaDiameter = FlameDiameter;
            }
        }

        var featherId = GuidHelper.CreateGuid<DropItemGroup>(Icarus.Number, 1);
        var darkPhoenix = gameConfiguration.Monsters.FirstOrDefault(monster => monster.Number == DarkPhoenixNumber);
        if (darkPhoenix is not null
            && gameConfiguration.DropItemGroups.FirstOrDefault(group => group.GetId() == featherId) is { } featherGroup)
        {
            featherGroup.Monster = darkPhoenix;
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
