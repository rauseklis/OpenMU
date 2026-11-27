// <copyright file="DarkSkeletonInvasionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The Dark Skeleton invasion: Dark Skeletons and Dark Reapers spawn all over the configured
/// map(s), which become dark for the players on them. The event ends after its duration or
/// as soon as all Dark Reapers are killed.
/// </summary>
[PlugIn]
[Display(Name = "Dark Skeleton Invasion", Description = "Dark Skeletons and Dark Reapers invade a darkened map. GM command: /startdark.")]
[Guid("4E8B2C71-9A3D-4F15-B6E0-2D7C5A1F9E83")]
public sealed class DarkSkeletonInvasionPlugIn : BaseInvasionPlugIn<DarkSkeletonInvasionConfiguration>, ISupportDefaultCustomConfiguration
{
    private volatile bool _isGameMasterStartPending;

    /// <inheritdoc />
    protected override ushort? AnnouncedMonsterId => this.Configuration is { } config
        ? config.EndWhenKilledMonsterIds.Count > 0 ? config.EndWhenKilledMonsterIds[0] : config.Mobs.FirstOrDefault()?.MonsterId
        : null;

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
    /// Starts the event on the next periodic check (about one second), as requested by a game master.
    /// </summary>
    public void StartByGameMaster()
    {
        this._isGameMasterStartPending = true;
        this.ForceStart();
    }

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
    protected override bool IsItTimeToStart(IGameContext gameContext)
        => this.IsEnabledOn(gameContext) && base.IsItTimeToStart(gameContext);

    /// <inheritdoc />
    protected override bool IsPreviousEventStillRunning(InvasionGameServerState state)
        => !this._isGameMasterStartPending && base.IsPreviousEventStillRunning(state);

    /// <inheritdoc />
    protected override InvasionGameServerState CreateState(IGameContext gameContext) => new DarkSkeletonInvasionState(gameContext);

    /// <inheritdoc />
    protected override async ValueTask OnPrepareEventAsync(InvasionGameServerState state)
    {
        this._isGameMasterStartPending = false;
        await base.OnPrepareEventAsync(state).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnStartedAsync(InvasionGameServerState state)
    {
        ((DarkSkeletonInvasionState)state).ResetBosses();
        await base.OnStartedAsync(state).ConfigureAwait(false);
        await state.Context.ForEachPlayerAsync(p => SendDarknessIfOnEventMapAsync(p, state, true)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnFinishedAsync(InvasionGameServerState state)
    {
        await state.Context.ForEachPlayerAsync(p => SendDarknessIfOnEventMapAsync(p, state, false)).ConfigureAwait(false);
        await base.OnFinishedAsync(state).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void OnMonsterCreated(Monster monster, InvasionGameServerState state)
    {
        if (this.Configuration?.EndWhenKilledMonsterIds.Contains((ushort)monster.Definition.Number) is not true)
        {
            return;
        }

        var darkState = (DarkSkeletonInvasionState)state;
        darkState.AddBoss();

        void OnBossDied(object? sender, DeathInformation e)
        {
            monster.Died -= OnBossDied;
            if (darkState.RemoveBoss() == 0 && darkState.State == PeriodicTaskState.Started)
            {
                // The periodic task finishes the event on its next check (within about a second).
                darkState.NextRunUtc = DateTime.UtcNow;
                darkState.Context.LoggerFactory.CreateLogger<DarkSkeletonInvasionPlugIn>()
                    .LogInformation("Dark Skeleton invasion: all bosses killed, the last one by {Killer}; ending the event.", e.KillerName);
            }
        }

        monster.Died += OnBossDied;
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

    /// <summary>
    /// The state of the Dark Skeleton invasion, which counts the living bosses.
    /// </summary>
    private sealed class DarkSkeletonInvasionState : InvasionGameServerState
    {
        private int _livingBosses;

        public DarkSkeletonInvasionState(IGameContext context)
            : base(context)
        {
        }

        public void ResetBosses() => Interlocked.Exchange(ref this._livingBosses, 0);

        public void AddBoss() => Interlocked.Increment(ref this._livingBosses);

        public int RemoveBoss() => Interlocked.Decrement(ref this._livingBosses);
    }
}
