// <copyright file="DarkSkeletonInvasionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;

using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The Dark Skeleton invasion: Dark Mages, Dark Skeletons and Dark Reapers spawn all over one main map
/// and, additionally, on the configured extra maps (Kalima). These maps become dark for the players on them.
/// The event ends after its duration or as soon as all Dark Reapers of the main map are killed.
/// </summary>
[PlugIn]
[Display(Name = "Dark Skeleton Invasion", Description = "Dark Skeletons and Dark Reapers invade a darkened map. GM commands: /startdarkdevias, /startdarklorencia, /startdarknoria, /startdarkelbeland, /stopdark.")]
[Guid("4E8B2C71-9A3D-4F15-B6E0-2D7C5A1F9E83")]
public sealed class DarkSkeletonInvasionPlugIn : BaseInvasionPlugIn<DarkSkeletonInvasionConfiguration>, ISupportDefaultCustomConfiguration
{
    private readonly ConditionalWeakTable<GameMap, SpawnPointCache> _spawnPoints = new();

    /// <inheritdoc />
    public object CreateDefaultConfig() => DarkSkeletonInvasionConfiguration.Default;

    /// <summary>
    /// Determines whether the event may run on the specified game context (game server).
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns><c>true</c> if the event is enabled on this game server.</returns>
    public bool IsEnabledOn(IGameContext gameContext)
    {
        var serverIds = (this.Configuration ?? DarkSkeletonInvasionConfiguration.Default).GameServerIds;
        return serverIds.Count == 0
               || gameContext is not IGameServerContext serverContext
               || serverIds.Contains(serverContext.Id);
    }

    /// <summary>
    /// Determines whether the event is currently announced or running on the specified game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns><c>true</c> if the event is active.</returns>
    public bool IsRunning(IGameContext gameContext)
        => this.GetStateByGameContext(gameContext).State != PeriodicTaskState.NotStarted;

    /// <summary>
    /// Starts the event with the specified main map on the specified game context (game server) on the next
    /// periodic check (about one second), as requested by a game master. Other game servers are not affected.
    /// </summary>
    /// <param name="gameContext">The game context of the game server on which the event should start.</param>
    /// <param name="mainMapId">The main map of the event.</param>
    public void StartByGameMaster(IGameContext gameContext, ushort mainMapId)
    {
        var state = (DarkSkeletonInvasionState)this.GetStateByGameContext(gameContext);
        state.RequestedMainMapId = mainMapId;
        state.IsStartRequested = true;
        state.NextRunUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Ends the event on the specified game context (game server) on the next periodic check (about one second),
    /// as requested by a game master. The remaining monsters disappear and the light comes back.
    /// </summary>
    /// <param name="gameContext">The game context of the game server on which the event should end.</param>
    /// <returns><c>true</c> if the event was announced or running and is now ending.</returns>
    public bool StopByGameMaster(IGameContext gameContext)
    {
        var state = (DarkSkeletonInvasionState)this.GetStateByGameContext(gameContext);
        if (state.State == PeriodicTaskState.NotStarted)
        {
            return false;
        }

        state.IsStopRequested = true;
        state.NextRunUtc = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Gets the main map of the announced or running event on the specified game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The main map id, or <c>null</c> if the event isn't active.</returns>
    public ushort? GetMainMapId(IGameContext gameContext)
        => ((DarkSkeletonInvasionState)this.GetStateByGameContext(gameContext)).MainMapId;

    /// <inheritdoc />
    public override async ValueTask ObjectAddedToMapAsync(GameMap map, ILocateable addedObject)
    {
        await base.ObjectAddedToMapAsync(map, addedObject).ConfigureAwait(false);
        if (addedObject is Player player)
        {
            var state = this.GetStateByGameContext(player.GameContext);
            if (state.State == PeriodicTaskState.Started && state.MapIds.Contains(map.MapId))
            {
                await SendDarknessAsync(player, true).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Only tiles which can be reached on foot: from the town on the main maps, where the
    /// <see cref="DarkSkeletonInvasionConfiguration.TownDistanceMapIds"/> also keep a distance to the town;
    /// and from the entrance on the extra maps (Kalima), whose few safezone tiles lie on the cut-off map edge.
    /// </remarks>
    protected override Point? GetRandomSpawnCoordinate(GameMap gameMap)
    {
        var config = this.Configuration;
        var mapId = gameMap.MapId;
        Point[] points;
        if (config is not null && config.TownDistanceMapIds.Contains(mapId))
        {
            points = this.GetSpawnPoints(gameMap, false, config.MinimumTownDistance);
        }
        else if (config is not null && config.ExtraMobs.Any(mob => mob.MapIds.Contains(mapId)))
        {
            points = this.GetSpawnPoints(gameMap, true, config.MinimumEntranceDistance);
            if (points.Length == 0)
            {
                // Rather no monster than one which can't be found.
                return null;
            }
        }
        else
        {
            return gameMap.Terrain.RandomCoordinateReachableFromSafezone;
        }

        return points.Length > 0
            ? points[Random.Shared.Next(points.Length)]
            : gameMap.Terrain.RandomCoordinateReachableFromSafezone;
    }

    /// <inheritdoc />
    protected override bool IsItTimeToStart(IGameContext gameContext)
        => this.IsEnabledOn(gameContext)
           && (((DarkSkeletonInvasionState)this.GetStateByGameContext(gameContext)).IsStartRequested || base.IsItTimeToStart(gameContext));

    /// <inheritdoc />
    protected override bool IsPreviousEventStillRunning(InvasionGameServerState state)
        => !((DarkSkeletonInvasionState)state).IsStartRequested && base.IsPreviousEventStillRunning(state);

    /// <inheritdoc />
    protected override InvasionGameServerState CreateState(IGameContext gameContext) => new DarkSkeletonInvasionState(gameContext);

    /// <inheritdoc />
    protected override ValueTask OnPrepareEventAsync(InvasionGameServerState state)
    {
        var darkState = (DarkSkeletonInvasionState)state;
        var requestedMainMapId = darkState.RequestedMainMapId;
        darkState.RequestedMainMapId = null;
        darkState.IsStartRequested = false;
        darkState.IsStopRequested = false;
        darkState.AreAllBossesKilled = false;

        var config = this.Configuration;
        if (config is null)
        {
            return ValueTask.CompletedTask;
        }

        var mainMapId = requestedMainMapId ?? SelectDailyMainMap(config);
        darkState.MainMapId = mainMapId;
        if (mainMapId is { } mapId)
        {
            foreach (var mob in config.Mobs)
            {
                state.RegisterMap(mapId, mob.MonsterId);
            }

            state.SetAnnouncedMaps([mapId]);
        }

        foreach (var extraMob in config.ExtraMobs)
        {
            foreach (var extraMapId in extraMob.MapIds)
            {
                state.RegisterMap(extraMapId);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override async ValueTask SpawnMobsOnMapsAsync(InvasionGameServerState state)
    {
        var config = this.Configuration;
        if (config is null)
        {
            return;
        }

        var darkState = (DarkSkeletonInvasionState)state;
        if (darkState.MainMapId is { } mainMapId)
        {
            await this.SpawnMobsAsync(state.Context, mainMapId, config.Mobs).ConfigureAwait(false);
        }

        foreach (var extraMob in config.ExtraMobs)
        {
            if (extraMob.MapIds.Count == 0)
            {
                continue;
            }

            if (extraMob.IsSpawnOnAllMaps)
            {
                foreach (var extraMapId in extraMob.MapIds)
                {
                    await this.SpawnMobsAsync(state.Context, extraMapId, [extraMob]).ConfigureAwait(false);
                }
            }
            else
            {
                var extraMapId = extraMob.MapIds[Random.Shared.Next(extraMob.MapIds.Count)];
                await this.SpawnMobsAsync(state.Context, extraMapId, [extraMob]).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnStartedAsync(InvasionGameServerState state)
    {
        var darkState = (DarkSkeletonInvasionState)state;
        darkState.ResetBosses();
        if (darkState.IsStopRequested)
        {
            // A game master stopped it during the announcement: end it on the next check without monsters or darkness.
            state.NextRunUtc = DateTime.UtcNow;
            return;
        }

        await base.OnStartedAsync(state).ConfigureAwait(false);
        await state.Context.ForEachPlayerAsync(p => SendDarknessIfOnEventMapAsync(p, state, true)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnFinishedAsync(InvasionGameServerState state)
    {
        await state.Context.ForEachPlayerAsync(p => SendDarknessIfOnEventMapAsync(p, state, false)).ConfigureAwait(false);
        await base.OnFinishedAsync(state).ConfigureAwait(false);
        var darkState = (DarkSkeletonInvasionState)state;
        darkState.IsStopRequested = false;
        darkState.AreAllBossesKilled = false;
        darkState.MainMapId = null;
    }

    /// <inheritdoc />
    protected override LocalizedString GetEndMessage(InvasionGameServerState state, DarkSkeletonInvasionConfiguration configuration)
        => ((DarkSkeletonInvasionState)state).AreAllBossesKilled
            ? configuration.AllBossesKilledMessage
            : base.GetEndMessage(state, configuration);

    /// <inheritdoc />
    protected override void OnMonsterInitializing(Monster monster, GameMap gameMap)
    {
        if (this.Configuration is not { } config
            || gameMap.ServerId is not { } serverId
            || !config.WeakerGameServerIds.Contains(serverId)
            || config.WeakerStrengthPercentage <= 0
            || Math.Abs(config.WeakerStrengthPercentage - 100) < 0.01f)
        {
            return;
        }

        // One element per monster; it lives as long as this event monster.
        var factor = new ConstantElement(config.WeakerStrengthPercentage / 100f, AggregateType.Multiplicate);
        monster.Attributes.AddElement(factor, Stats.MaximumHealth);
        monster.Attributes.AddElement(factor, Stats.MinimumPhysBaseDmg);
        monster.Attributes.AddElement(factor, Stats.MaximumPhysBaseDmg);
        monster.Attributes.AddElement(factor, Stats.DefenseBase);
        monster.Attributes.AddElement(factor, Stats.AttackRatePvm);
        monster.Attributes.AddElement(factor, Stats.DefenseRatePvm);
    }

    /// <inheritdoc />
    protected override void OnMonsterCreated(Monster monster, InvasionGameServerState state)
    {
        var darkState = (DarkSkeletonInvasionState)state;
        if (this.Configuration?.EndWhenKilledMonsterIds.Contains((ushort)monster.Definition.Number) is not true
            || monster.CurrentMap.MapId != darkState.MainMapId)
        {
            // Only the bosses of the main map end the event; the ones on the extra maps (Kalima) don't count.
            return;
        }

        darkState.AddBoss();

        void OnBossDied(object? sender, DeathInformation e)
        {
            monster.Died -= OnBossDied;
            if (darkState.RemoveBoss() == 0 && darkState.State == PeriodicTaskState.Started)
            {
                // The periodic task finishes the event on its next check (within about a second).
                darkState.AreAllBossesKilled = true;
                darkState.NextRunUtc = DateTime.UtcNow;
                darkState.Context.LoggerFactory.CreateLogger<DarkSkeletonInvasionPlugIn>()
                    .LogInformation("Dark Skeleton invasion: all bosses killed, the last one by {Killer}; ending the event.", e.KillerName);
            }
        }

        monster.Died += OnBossDied;
    }

    private static ushort? SelectDailyMainMap(DarkSkeletonInvasionConfiguration config)
    {
        var mapIds = config.Mobs.FirstOrDefault(mob => mob.MapIds.Count > 0)?.MapIds;
        return mapIds is null ? null : mapIds[Random.Shared.Next(mapIds.Count)];
    }

    private static async Task SendDarknessAsync(Player player, bool enabled)
    {
        try
        {
            await player.InvokeViewPlugInAsync<IMapEventStateUpdatePlugIn>(p => p.UpdateStateAsync(enabled, MapEventType.DarkSkeletonInvasion)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogDebug(ex, "Unexpected error sending the Dark Skeleton invasion darkness state.");
        }
    }

    private static Task SendDarknessIfOnEventMapAsync(Player player, InvasionGameServerState state, bool enabled)
    {
        if (player.CurrentMap is { } map
            && state.MapIds.Contains(map.MapId)
            && !player.PlayerState.CurrentState.IsDisconnectedOrFinished())
        {
            return SendDarknessAsync(player, enabled);
        }

        return Task.CompletedTask;
    }

    private Point[] GetSpawnPoints(GameMap gameMap, bool fromEntrance, byte minimumDistance)
    {
        if (this._spawnPoints.TryGetValue(gameMap, out var cache)
            && cache.FromEntrance == fromEntrance
            && cache.MinimumDistance == minimumDistance)
        {
            return cache.Points;
        }

        var startTiles = fromEntrance
            ? DarkInvasionSpawnPoints.GetEntranceTiles(gameMap.Definition)
            : DarkInvasionSpawnPoints.GetSafezoneTiles(gameMap.Terrain);
        var points = DarkInvasionSpawnPoints.Find(gameMap.Terrain, startTiles, minimumDistance);
        this._spawnPoints.AddOrUpdate(gameMap, new SpawnPointCache(fromEntrance, minimumDistance, points));
        return points;
    }

    /// <summary>
    /// The spawn points of a map, as found for the given settings.
    /// </summary>
    /// <param name="FromEntrance">Whether the points were found from the entrance instead of the town.</param>
    /// <param name="MinimumDistance">The minimum distance to the start area.</param>
    /// <param name="Points">The points.</param>
    private sealed record SpawnPointCache(bool FromEntrance, byte MinimumDistance, Point[] Points);

    /// <summary>
    /// The state of the Dark Skeleton invasion, which counts the living bosses of the main map.
    /// </summary>
    private sealed class DarkSkeletonInvasionState : InvasionGameServerState
    {
        private int _livingBosses;

        private volatile bool _isStopRequested;

        private volatile bool _isStartRequested;

        private volatile bool _areAllBossesKilled;

        public DarkSkeletonInvasionState(IGameContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether a game master requested to end the event.
        /// </summary>
        public bool IsStopRequested
        {
            get => this._isStopRequested;
            set => this._isStopRequested = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether a game master requested to start the event on this game server.
        /// </summary>
        public bool IsStartRequested
        {
            get => this._isStartRequested;
            set => this._isStartRequested = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the event ends because all bosses of the main map were killed.
        /// </summary>
        public bool AreAllBossesKilled
        {
            get => this._areAllBossesKilled;
            set => this._areAllBossesKilled = value;
        }

        /// <summary>
        /// Gets or sets the main map which a game master requested for the next start.
        /// </summary>
        public ushort? RequestedMainMapId { get; set; }

        /// <summary>
        /// Gets or sets the main map of the current run.
        /// </summary>
        public ushort? MainMapId { get; set; }

        public void ResetBosses() => Interlocked.Exchange(ref this._livingBosses, 0);

        public void AddBoss() => Interlocked.Increment(ref this._livingBosses);

        public int RemoveBoss() => Interlocked.Decrement(ref this._livingBosses);
    }
}
