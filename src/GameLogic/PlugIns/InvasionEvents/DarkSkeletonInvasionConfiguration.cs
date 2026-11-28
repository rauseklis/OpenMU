// <copyright file="DarkSkeletonInvasionConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;

using System.ComponentModel.DataAnnotations;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Configuration of the <see cref="DarkSkeletonInvasionPlugIn"/>.
/// </summary>
public class DarkSkeletonInvasionConfiguration : PeriodicInvasionConfiguration
{
    /// <summary>
    /// Gets the default configuration: every day at 20:00 (server time zone) for 15 minutes,
    /// 10 Dark Mages, 10 Dark Skeletons and 2 Dark Reapers spread over Devias (the main map), plus
    /// the extra monsters in Kalima 2 to 7, on game servers 0 and 1.
    /// On game server 1 the monsters have half of their strength.
    /// The event ends early when both Dark Reapers of the main map are killed.
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
        ExtraMobs = CreateDefaultExtraMobs(),
    };

    /// <summary>
    /// Gets or sets the monsters which additionally spawn on other maps (Kalima) whenever the event runs,
    /// with the count per map. They don't end the event, and their deaths aren't announced.
    /// </summary>
    /// <remarks>
    /// The <see cref="PeriodicInvasionConfiguration.Mobs"/> spawn on the main map of the run, which is one of
    /// their map ids for the daily start, or the map which a game master starts the event on.
    /// </remarks>
    [Display(Name = "Extra Monster Spawns (Kalima)", Description = "Spawn on these maps every time, count per map. They don't end the event.", Order = 12)]
    public IList<InvasionSpawnConfiguration> ExtraMobs { get; set; } = CreateDefaultExtraMobs();

    /// <summary>
    /// Gets or sets the main maps on which the monsters keep a distance to the town (safezone).
    /// </summary>
    [Display(Name = "Town Distance Map Ids", Description = "On these main maps the monsters don't spawn closer to the town than the minimum town distance.", Order = 13)]
    public IList<ushort> TownDistanceMapIds { get; set; } = [InvasionMaps.Lorencia, InvasionMaps.Noria, InvasionMaps.Elbeland];

    /// <summary>
    /// Gets or sets the minimum distance in tiles between a spawned monster and the town (safezone)
    /// on the <see cref="TownDistanceMapIds"/>.
    /// </summary>
    [Display(Name = "Minimum Town Distance", Description = "In tiles.", Order = 14)]
    public byte MinimumTownDistance { get; set; } = 20;

    /// <summary>
    /// Gets or sets the minimum distance in tiles between a spawned extra monster and the place where
    /// players enter its map (e.g. the Kalima entrance).
    /// </summary>
    [Display(Name = "Minimum Entrance Distance (Extra Maps)", Description = "In tiles.", Order = 15)]
    public byte MinimumEntranceDistance { get; set; } = 10;

    /// <summary>
    /// Gets or sets the message which is shown instead of the end message when the event ends
    /// because all Dark Reapers of the main map were killed.
    /// </summary>
    [Display(Name = "All Bosses Killed Message", Order = 16)]
    public LocalizedString AllBossesKilledMessage { get; set; } = "[{mapName}] All Dark Reapers have been defeated! The light returns.";

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

    private static IList<InvasionSpawnConfiguration> CreateDefaultExtraMobs() =>
    [
        new(InvasionMonsters.DarkMage, 5, [InvasionMaps.Kalima2, InvasionMaps.Kalima3], SpawnMapStrategy.AllMaps),
        new(InvasionMonsters.DarkSkeleton, 4, [InvasionMaps.Kalima4, InvasionMaps.Kalima5], SpawnMapStrategy.AllMaps),
        new(InvasionMonsters.DarkReaper, 3, [InvasionMaps.Kalima6, InvasionMaps.Kalima7], SpawnMapStrategy.AllMaps),
    ];
}
