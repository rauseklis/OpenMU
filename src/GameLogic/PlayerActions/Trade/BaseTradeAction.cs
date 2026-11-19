// <copyright file="BaseTradeAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Trade;

using System.Diagnostics.Metrics;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Action to cancel a trade.
/// </summary>
public class BaseTradeAction
{
    private static readonly Meter TradeMeter = new(MeterName);

    /// <summary>
    /// Gets the name of the meter of this class.
    /// </summary>
    internal static string MeterName => typeof(BaseTradeAction).Namespace ?? nameof(BaseTradeAction);

    /// <summary>
    /// Gets the counter for finished trades.
    /// </summary>
    protected static Counter<int> FinishedTrades { get; } = TradeMeter.CreateCounter<int>("FinishedTradesCount");

    /// <summary>
    /// Gets the counter for cancelled trades.
    /// </summary>
    private static Counter<int> CancelledTrades { get; } = TradeMeter.CreateCounter<int>("CancelledTradesCount");

    /// <summary>
    /// Cancels the trade.
    /// </summary>
    /// <param name="trader">The trader.</param>
    /// <param name="checkState">If set to <c>true</c>, the player state is checked. Otherwise, it's ignored.</param>
    protected async ValueTask CancelTradeAsync(ITrader trader, bool checkState = true)
    {
        CancelledTrades.Add(1);
        await using (var context = await trader.PlayerState.TryBeginAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false))
        {
            if (checkState && !context.Allowed)
            {
                return;
            }

            if (trader.BackupInventory != null)
            {
                // The offered money was already taken from the character's balance, so it has to be given back.
                // The backup only exists while a trade is open, which prevents a refund after a finished trade.
                if (trader.TradingMoney > 0)
                {
                    trader.Money += trader.TradingMoney;
                }

                trader.Inventory!.Clear();
                trader.BackupInventory.RestoreItemStates();
                foreach (var item in trader.BackupInventory.Items)
                {
                    try
                    {
                        if (!await trader.Inventory.AddItemAsync(item.ItemSlot, item).ConfigureAwait(false)
                            && !await trader.Inventory.AddItemAsync(item).ConfigureAwait(false))
                        {
                            trader.Logger.LogError(
                                "Failed to restore item {item} during trade cancellation for {trader}.",
                                item,
                                trader.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        trader.Logger.LogError(
                            ex,
                            "Error restoring item {item} during trade cancellation for {trader}.",
                            item,
                            trader.Name);
                    }
                }

                trader.BackupInventory = null;
            }

            trader.TradingMoney = 0;
        }

        this.ResetTradeState(trader);
    }

    /// <summary>
    /// Resets the trade.
    /// </summary>
    /// <param name="trader">The trader.</param>
    protected void ResetTradeState(ITrader trader)
    {
        trader.TradingPartner = null;
        trader.BackupInventory = null;
        trader.TemporaryStorage!.Clear();
    }

    /// <summary>
    /// Sends the message to the trader.
    /// </summary>
    /// <param name="trader">The trader.</param>
    /// <param name="messageKey">The key of the message in <see cref="PlayerMessage"/>.</param>
    protected async ValueTask SendMessageAsync(ITrader trader, string messageKey)
    {
        if (trader is Player player)
        {
            try
            {
                await player.ShowLocalizedBlueMessageAsync(messageKey).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                trader.Logger.LogError(ex, "Error sending a message");
            }
        }
    }
}
