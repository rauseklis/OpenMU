// <copyright file="JewelRainPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.JewelRain;

using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Runs the Jewel Rain event: a scheduled countdown followed by batches of items
/// which drop on a map, one step after another.
/// </summary>
[PlugIn]
[Display(Name = "Jewel Rain Event", Description = "Announces and runs the scheduled Jewel Rain drop event.")]
[Guid("7B3E1F42-5C9A-4D68-9E21-0A6F4C8D3B15")]
public sealed class JewelRainPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<JewelRainConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly ConditionalWeakTable<IGameContext, JewelRainScheduler> Schedulers = new();

    private volatile bool _isStartForced;

    /// <inheritdoc />
    public JewelRainConfiguration? Configuration { get; set; }

    /// <summary>
    /// Requests an immediate start of the event on the specified game context, without countdown.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    public static void RequestStart(IGameContext gameContext) => GetScheduler(gameContext).RequestForceStart();

    /// <inheritdoc />
    public object CreateDefaultConfig() => JewelRainConfiguration.Default;

    /// <inheritdoc />
    public void ForceStart() => this._isStartForced = true;

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var logger = gameContext.LoggerFactory.CreateLogger<JewelRainPlugIn>();
        try
        {
            var config = this.Configuration ??= JewelRainConfiguration.Default;
            var scheduler = GetScheduler(gameContext);
            if (this._isStartForced)
            {
                this._isStartForced = false;
                scheduler.RequestForceStart();
            }

            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, gameContext.ServerTimeZone);
            foreach (var action in scheduler.Advance(config, nowLocal))
            {
                await this.ExecuteAsync(gameContext, config, action, logger).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in the Jewel Rain event.");
        }
    }

    private static JewelRainScheduler GetScheduler(IGameContext gameContext) => Schedulers.GetValue(gameContext, _ => new JewelRainScheduler());

    private async ValueTask ExecuteAsync(GameContext gameContext, JewelRainConfiguration config, JewelRainAction action, ILogger logger)
    {
        switch (action.Kind)
        {
            case JewelRainActionKind.Countdown:
                await BroadcastAsync(gameContext, config.CountdownMessage, action.Value).ConfigureAwait(false);
                break;
            case JewelRainActionKind.Start:
                logger.LogInformation("Jewel Rain started on map {Map}.", config.MapNumber);
                await BroadcastAsync(gameContext, config.StartMessage, 0).ConfigureAwait(false);
                break;
            case JewelRainActionKind.Drop:
                await DropStepAsync(gameContext, config, action.Value, logger).ConfigureAwait(false);
                break;
            case JewelRainActionKind.End:
                logger.LogInformation("Jewel Rain ended.");
                await BroadcastAsync(gameContext, config.EndMessage, 0).ConfigureAwait(false);
                break;
            case JewelRainActionKind.Missed:
                logger.LogWarning("A scheduled Jewel Rain start was noticed too late or overlapped a running event and was skipped.");
                break;
        }
    }

    private static async ValueTask BroadcastAsync(GameContext gameContext, LocalizedString message, int minutes)
    {
        await gameContext.ForEachPlayerAsync(async player =>
        {
            var text = (message.GetTranslation(player.Culture) ?? string.Empty)
                .Replace("{minutes}", minutes.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(text, MessageType.GoldenCenter)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogDebug(ex, "Unexpected error sending a Jewel Rain message.");
            }
        }).ConfigureAwait(false);
    }

    private static async ValueTask DropStepAsync(GameContext gameContext, JewelRainConfiguration config, int stepIndex, ILogger logger)
    {
        if (stepIndex >= config.Steps.Count)
        {
            return;
        }

        var map = await gameContext.GetMapAsync(config.MapNumber, false).ConfigureAwait(false);
        if (map is null)
        {
            logger.LogWarning("Jewel Rain: map {Map} is not available on this game server.", config.MapNumber);
            return;
        }

        var step = config.Steps[stepIndex];
        for (var i = 0; i < step.Amount; i++)
        {
            var cell = JewelRainDropHelper.TryGetDropCell(map.Terrain, config);
            if (cell is null)
            {
                logger.LogWarning("Jewel Rain: no walkable cell in the drop area on map {Map}.", config.MapNumber);
                return;
            }

            var item = JewelRainDropHelper.TryCreateItem(gameContext, step);
            if (item is null)
            {
                logger.LogWarning("Jewel Rain: item {Group}/{Number} does not exist.", step.Group, step.Number);
                return;
            }

            await map.AddAsync(new DroppedItem(item, cell.Value, map, null, null)).ConfigureAwait(false);
        }
    }
}
