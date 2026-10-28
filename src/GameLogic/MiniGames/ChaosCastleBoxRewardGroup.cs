// <copyright file="ChaosCastleBoxRewardGroup.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Expands the two mixed CC box pools into transient, equally selected slots.
/// The persistent pool contains each item once; repeated slots never reach the database.
/// </summary>
internal sealed class ChaosCastleBoxRewardGroup : DropItemGroup
{
    private const byte BoxGroup = 14;
    private const short LuckBox = 11;
    private const short SilverBox = 124;
    private const short GoldenBox = 123;
    private const int CastleFiveLuckSlots = 4;
    private const int CastleFiveTotalSlots = 5;
    private const int CastleSevenSilverSlots = 17;
    private const int CastleSevenTotalSlots = 20;

    private ChaosCastleBoxRewardGroup(DropItemGroup source, ItemDefinition first, ItemDefinition second, int firstSlots, int totalSlots)
    {
        this.Description = source.Description;
        this.Chance = source.Chance;
        this.ItemType = source.ItemType;
        this.ItemLevel = source.ItemLevel;
        this.PossibleItems = Enumerable.Repeat(first, firstSlots).Concat(Enumerable.Repeat(second, totalSlots - firstSlots)).ToList();
    }

    /// <summary>Preserves other reward pools and expands only the two approved mixed box pools.</summary>
    /// <param name="source">The configured reward group.</param>
    /// <param name="level">The Chaos Castle level.</param>
    /// <returns>The transient weighted group, or the unchanged configured group.</returns>
    internal static DropItemGroup Create(DropItemGroup source, byte level)
    {
        if (level is not (5 or 7) || source.ItemType != SpecialItemType.RandomItem || source.ItemLevel != 0
            || source.PossibleItems.Count != 2 || source.PossibleItems.Any(item => item.Group != BoxGroup))
        {
            return source;
        }

        var first = source.PossibleItems.SingleOrDefault(item => item.Number == (level == 5 ? LuckBox : SilverBox));
        var second = source.PossibleItems.SingleOrDefault(item => item.Number == (level == 5 ? SilverBox : GoldenBox));
        if (first is null || second is null)
        {
            return source;
        }

        return level == 5
            ? new ChaosCastleBoxRewardGroup(source, first, second, CastleFiveLuckSlots, CastleFiveTotalSlots)
            : new ChaosCastleBoxRewardGroup(source, first, second, CastleSevenSilverSlots, CastleSevenTotalSlots);
    }
}
