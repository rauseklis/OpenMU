// <copyright file="QuestItemDropRatesUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>Makes the items of the class quests much rarer.</summary>
[PlugIn]
[Guid("8E3B4D1A-62C7-4F09-A5D8-1B7E90C3F2A4")]
public sealed class QuestItemDropRatesUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Rarer class quest items";

    /// <inheritdoc />
    public override string Description => "Quest 0 and 1 items drop about every 1500 kills, quest 2 and 3 items about every 3000 kills; the Evidence of Strength boss drops are halved.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.QuestItemDropRates;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var quests = gameConfiguration.Monsters.SelectMany(monster => monster.Quests)
            .Where(quest => quest.Group == QuestItemDropRates.LegacyQuestGroup)
            .Distinct();
        foreach (var quest in quests)
        {
            foreach (var requirement in quest.RequiredItems.Where(item => item.DropItemGroup is not null && item.Item is not null))
            {
                var chance = quest.Number == 4
                    ? QuestItemDropRates.ForEvidenceOfStrength(requirement.Item!.Number)
                    : QuestItemDropRates.ForQuest(quest.Number);
                if (chance is { } value)
                {
                    requirement.DropItemGroup!.Chance = value;
                }
            }
        }

        return ValueTask.CompletedTask;
    }
}
