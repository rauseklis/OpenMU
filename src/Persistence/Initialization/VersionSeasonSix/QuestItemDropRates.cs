// <copyright file="QuestItemDropRates.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

/// <summary>
/// The drop chances (per monster kill) of the items which the legacy class quests require.
/// </summary>
public static class QuestItemDropRates
{
    /// <summary>
    /// The quest group of the legacy class quests.
    /// </summary>
    public const byte LegacyQuestGroup = 0;

    /// <summary>
    /// Quests 0 (Scroll of Emperor) and 1 (Treasures of MU), first class change: one piece about every 1500 kills.
    /// </summary>
    public const double FirstClassChange = 1.0 / 1500.0;

    /// <summary>
    /// Quests 2 (Hero Status) and 3 (Secret of the Dark Stone): one piece about every 3000 kills.
    /// </summary>
    public const double HeroAndDarkStone = 1.0 / 3000.0;

    /// <summary>
    /// The previous chance of every item of the quests 0 to 3 (0.1 percent).
    /// </summary>
    public const double PreviousDefault = 10.0 / 10000.0;

    /// <summary>
    /// Gets the chance of the item of a legacy quest which drops from any monster of a level range.
    /// </summary>
    /// <param name="questNumber">The quest number.</param>
    /// <returns>The chance per kill, or <see langword="null"/> if the quest does not use this kind of drop.</returns>
    public static double? ForQuest(short questNumber) => questNumber switch
    {
        0 or 1 => FirstClassChange,
        2 or 3 => HeroAndDarkStone,
        _ => null,
    };

    /// <summary>
    /// Gets the chance of an item of the quest 4 (Evidence of Strength), which drops from one specific boss monster.
    /// </summary>
    /// <param name="itemNumber">The number of the quest item (group 14).</param>
    /// <returns>The chance per kill of that monster, or <see langword="null"/> for other items.</returns>
    public static double? ForEvidenceOfStrength(short itemNumber) => itemNumber switch
    {
        65 => 1.5 / 100.0, // Flame of Death Beam Knight (was 3 %)
        66 => 2.0 / 100.0, // Horn of Hell Maine (was 4 %)
        67 => 2.5 / 100.0, // Feather of Dark Phoenix (was 5 %)
        _ => null,
    };
}
