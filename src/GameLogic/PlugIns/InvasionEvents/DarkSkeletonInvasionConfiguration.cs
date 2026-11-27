// <copyright file="DarkSkeletonInvasionConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration of the <see cref="DarkSkeletonInvasionPlugIn"/>.
/// </summary>
public class DarkSkeletonInvasionConfiguration : PeriodicInvasionConfiguration
{
    /// <summary>
    /// Gets the default configuration: every day at 20:00 (server time zone) for 15 minutes,
    /// 10 Dark Mages, 10 Dark Skeletons and 2 Dark Reapers spread over Devias, on game servers 0 and 1.
    /// On game server 1 the monsters have half of their strength.
    /// The event ends early when both Dark Reapers are killed.
    /// </summary>
    public static DarkSkeletonInvasionConfiguration Default => new()
    {
        TaskDuration = TimeSpan.FromMinutes(15),
        PreStartMessageDelay = TimeSpan.FromSeconds(3),
        StartMessage = "[{mapName}] Dark Skeleton invasion! The light fades...",
        EndMessage = "[{mapName}] Dark Skeleton invasion has ended.",
        Timetable = [new TimeOnly(20, 0)],
        GameServerIds = [0, 1],
        WeakerGameServerIds = [1],
        WeakerStrengthPercentage = 50,
        EndWhenKilledMonsterIds = [InvasionMonsters.DarkReaper],
        Mobs =
        [
            new(InvasionMonsters.DarkMage, 10, [InvasionMaps.Devias], SpawnMapStrategy.AllMaps),
            new(InvasionMonsters.DarkSkeleton, 10, [InvasionMaps.Devias], SpawnMapStrategy.AllMaps),
            new(InvasionMonsters.DarkReaper, 2, [InvasionMaps.Devias], SpawnMapStrategy.AllMaps, announceDeath: true),
        ],
    };

    /// <summary>
    /// Gets or sets the ids of the game servers on which the event runs.
    /// When empty, it runs on every game server.
    /// </summary>
    [Display(Name = "Game Server Ids", Description = "The event runs only on these game servers (empty = all).", Order = 8)]
    public IList<byte> GameServerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the monster ids which end the event early when all their spawned
    /// instances are killed. When empty, the event always runs its full duration.
    /// </summary>
    [Display(Name = "End When Killed (Monster Ids)", Description = "The event ends early when all spawned monsters of these ids are killed.", Order = 9)]
    public IList<ushort> EndWhenKilledMonsterIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the game servers on which the event monsters are weaker.
    /// </summary>
    [Display(Name = "Weaker On Game Server Ids", Description = "On these game servers the event monsters have only the 'Weaker Strength' percentage of their values.", Order = 10)]
    public IList<byte> WeakerGameServerIds { get; set; } = [1];

    /// <summary>
    /// Gets or sets the percentage of health, damage, defense, attack rate and defense rate which the
    /// event monsters have on the <see cref="WeakerGameServerIds"/> (50 = half).
    /// </summary>
    [Display(Name = "Weaker Strength (%)", Description = "Health, damage, defense, attack rate and defense rate in percent of the normal values (50 = half).", Order = 11)]
    public float WeakerStrengthPercentage { get; set; } = 50;
}
