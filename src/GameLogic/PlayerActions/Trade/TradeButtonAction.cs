// <copyright file="TradeButtonAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Trade;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Trade;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Action to change the trade button state.
/// </summary>
public class TradeButtonAction : BaseTradeAction
{
    /// <summary>
    /// Tries to change the trade button change to the new <paramref name="tradeButtonState"/>.
    /// </summary>
    /// <param name="trader">The trader.</param>
    /// <param name="tradeButtonState">The new state of the trade button.</param>
    public async ValueTask TradeButtonChangedAsync(ITrader trader, TradeButtonState tradeButtonState)
    {
        using var loggerScope = (trader as Player)?.Logger.BeginScope(this.GetType());
        var success = (tradeButtonState == TradeButtonState.Checked && await trader.PlayerState.TryAdvanceToAsync(PlayerState.TradeButtonPressed).ConfigureAwait(false))
                      || (tradeButtonState == TradeButtonState.Unchecked && await trader.PlayerState.TryAdvanceToAsync(PlayerState.TradeOpened).ConfigureAwait(false));
        if (!success)
        {
            return;
        }

        var tradingPartner = trader.TradingPartner;

        if (trader.PlayerState.CurrentState == PlayerState.TradeButtonPressed
            && tradingPartner is not null
            && tradingPartner.PlayerState.CurrentState == PlayerState.TradeButtonPressed)
        {
            TradeResult result = await this.InternalFinishTradeAsync(trader, tradingPartner).ConfigureAwait(false);
            if (result == TradeResult.Success)
            {
                await SaveAfterTradeAsync(trader, tradingPartner).ConfigureAwait(false);
            }

            if (result != TradeResult.Success)
            {
                await this.CancelTradeAsync(tradingPartner, false).ConfigureAwait(false);
                await this.CancelTradeAsync(trader, false).ConfigureAwait(false);
                (trader as Player)?.Logger.LogDebug($"Cancelled the trade because of unfinished state. trader: {trader.Name}, partner:{tradingPartner.Name}");
            }

            await trader.InvokeViewPlugInAsync<ITradeFinishedPlugIn>(p => p.TradeFinishedAsync(result)).ConfigureAwait(false);
            await tradingPartner.InvokeViewPlugInAsync<ITradeFinishedPlugIn>(p => p.TradeFinishedAsync(result)).ConfigureAwait(false);
        }
        else if (tradingPartner is not null)
        {
            await tradingPartner.InvokeViewPlugInAsync<IChangeTradeButtonStatePlugIn>(p => p.ChangeTradeButtonStateAsync(TradeButtonState.Checked)).ConfigureAwait(false);
        }
        else
        {
            // nothing to do.
        }
    }

    /// <summary>
    /// Saves both traders right after a finished trade. The traded items are stored in an own step while the
    /// payment only changed in memory; without this save, a stop of the server shortly after the trade could keep
    /// the items moved but not the payment. It runs after the trade state was left, so a failure here can never
    /// turn the finished trade into a cancelled one (which would restore the old inventories).
    /// </summary>
    /// <param name="trader">The trader who pressed the button last.</param>
    /// <param name="tradingPartner">The trading partner.</param>
    private static async ValueTask SaveAfterTradeAsync(ITrader trader, ITrader tradingPartner)
    {
        foreach (var party in new[] { trader, tradingPartner })
        {
            try
            {
                if (!await party.SaveProgressAsync().ConfigureAwait(false))
                {
                    (party as Player)?.Logger.LogWarning("Saving {Trader} after a finished trade did not succeed; the next regular save will retry.", party.Name);
                }
            }
            catch (Exception ex)
            {
                (party as Player)?.Logger.LogError(ex, "Saving {Trader} after a finished trade failed; the next regular save will retry.", party.Name);
            }
        }
    }

    private static void ReportZen(ITrader from, ITrader to, int amount)
    {
        if (amount > 0)
        {
            PlugIns.ItemCustodyPlugIn.Report(from.GameContext, Combat.CustodyEventKind.ZenTraded, null, (from as Player)?.SelectedCharacter, from.Name, (to as Player)?.SelectedCharacter, to.Name, amount);
        }
    }

    private static bool CanReceiveMoney(ITrader receiver, ITrader sender)
    {
        var maximum = receiver.GameContext.Configuration?.MaximumInventoryMoney ?? int.MaxValue;
        return (long)receiver.Money + sender.TradingMoney <= maximum;
    }

    private static async ValueTask<bool> TryAddItemsOfTradingPartnerAsync(ITrader trader)
    {
        if (trader.TradingPartner?.TemporaryStorage?.Items.Any() ?? false)
        {
            return await trader.Inventory!.TryTakeAllAsync(trader.TradingPartner.TemporaryStorage!).ConfigureAwait(false);
        }

        return true;
    }

    private async ValueTask<TradeResult> InternalFinishTradeAsync(ITrader trader, ITrader tradingPartner)
    {
        await using var context = await trader.PlayerState.TryBeginAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        await using var partnerContext = await tradingPartner.PlayerState.TryBeginAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        if (!context.Allowed || !partnerContext.Allowed)
        {
            context.Allowed = false;
            partnerContext.Allowed = false;
            (trader as Player)?.Logger.LogDebug($"Unexpected player states. {trader.Name}:{trader.PlayerState}, {tradingPartner.Name}:{tradingPartner.PlayerState}");
            return TradeResult.Cancelled;
        }

        using var itemContext = trader.GameContext.PersistenceContextProvider.CreateNewTradeContext();
        var traderItems = trader.TemporaryStorage!.Items.ToList();
        var tradePartnerItems = tradingPartner.TemporaryStorage!.Items.ToList();

        // Checked here, and not only when an item is moved into the trade: the temporary storage
        // is shared with other windows (e.g. the chaos machine), so it may already contain items.
        // Both are checked, so that each trader gets the message about their own items.
        var traderItemsAreTradable = await this.CheckItemsAreTradableAsync(trader, traderItems).ConfigureAwait(false);
        var partnerItemsAreTradable = await this.CheckItemsAreTradableAsync(tradingPartner, tradePartnerItems).ConfigureAwait(false);
        if (!traderItemsAreTradable || !partnerItemsAreTradable)
        {
            return TradeResult.Cancelled;
        }

        // Checked before anything is moved: adding the zen of the partner must not exceed the limit of the receiver
        // (and must never wrap around the range of the number). Both traders are told, like for a full inventory.
        if (!CanReceiveMoney(trader, tradingPartner) || !CanReceiveMoney(tradingPartner, trader))
        {
            await this.SendMessageAsync(trader, nameof(PlayerMessage.InventoryFull)).ConfigureAwait(false);
            await this.SendMessageAsync(tradingPartner, nameof(PlayerMessage.InventoryFull)).ConfigureAwait(false);
            return TradeResult.FailedByFullInventory;
        }

        this.AttachItemsToPersistenceContext(traderItems, itemContext);
        this.AttachItemsToPersistenceContext(tradePartnerItems, itemContext);

        if (!await TryAddItemsOfTradingPartnerAsync(trader).ConfigureAwait(false) || !await TryAddItemsOfTradingPartnerAsync(tradingPartner).ConfigureAwait(false))
        {
            // TODO: Maybe notify which player has the full inventory.
            await this.SendMessageAsync(trader, nameof(PlayerMessage.InventoryFull)).ConfigureAwait(false);
            await this.SendMessageAsync(tradingPartner, nameof(PlayerMessage.InventoryFull)).ConfigureAwait(false);
            return TradeResult.FailedByFullInventory;
        }

        try
        {
            this.DetachItemsFromPersistenceContext(traderItems, trader.PersistenceContext);
            this.DetachItemsFromPersistenceContext(tradePartnerItems, trader.TradingPartner!.PersistenceContext);
            await itemContext.SaveChangesAsync().ConfigureAwait(false);
            this.AttachItemsToPersistenceContext(traderItems, trader.TradingPartner.PersistenceContext);
            this.AttachItemsToPersistenceContext(tradePartnerItems, trader.PersistenceContext);
            trader.Money += trader.TradingPartner.TradingMoney;
            trader.TradingPartner.Money += trader.TradingMoney;
            await trader.TradingPartner.InvokeViewPlugInAsync<IChangeTradeButtonStatePlugIn>(p => p.ChangeTradeButtonStateAsync(TradeButtonState.Checked)).ConfigureAwait(false);
            this.ResetTradeState(trader.TradingPartner);
            this.ResetTradeState(trader);
            this.CallPlugIn(traderItems, trader, tradingPartner);
            this.CallPlugIn(tradePartnerItems, tradingPartner, trader);
            ReportZen(trader, tradingPartner, trader.TradingMoney);
            ReportZen(tradingPartner, trader, tradingPartner.TradingMoney);
            FinishedTrades.Add(1);
            return TradeResult.Success;
        }
        catch (Exception exception)
        {
            await this.SendMessageAsync(trader, nameof(PlayerMessage.UnexpectedErrorDuringClosingTrade)).ConfigureAwait(false);
            await this.SendMessageAsync(tradingPartner, nameof(PlayerMessage.UnexpectedErrorDuringClosingTrade)).ConfigureAwait(false);
            context.Allowed = false;
            partnerContext.Allowed = false;
            (trader as Player)?.Logger.LogError(exception, $"An unexpected error occured during closing the trade. trader: {trader.Name}, partner:{tradingPartner.Name}");
            return TradeResult.Cancelled;
        }
    }

    private async ValueTask<bool> CheckItemsAreTradableAsync(ITrader trader, IEnumerable<Item> items)
    {
        var notTradable = items.FirstOrDefault(item => item.Definition is { IsTradable: false });
        if (notTradable is null)
        {
            return true;
        }

        (trader as Player)?.Logger.LogWarning(
            "Trader {0} offered {1}, which its item definition doesn't allow to trade. The trade is cancelled.",
            trader.Name,
            notTradable);
        await this.SendMessageAsync(trader, nameof(PlayerMessage.ItemCannotBeTraded)).ConfigureAwait(false);
        return false;
    }

    private void CallPlugIn(IEnumerable<Item> items, ITrader source, ITrader target)
    {
        var point = target.GameContext.PlugInManager.GetPlugInPoint<IItemTradedToOtherPlayerPlugIn>();
        if (point is null)
        {
            return;
        }

        foreach (var item in items)
        {
            point.ItemTraded(source, target, item);
        }
    }

    private void AttachItemsToPersistenceContext(IEnumerable<Item> items, IContext itemContext)
    {
        foreach (var item in items)
        {
            itemContext.Attach(item);
        }
    }

    private void DetachItemsFromPersistenceContext(IEnumerable<Item> items, IContext itemContext)
    {
        foreach (var item in items)
        {
            itemContext.Detach(item);
        }
    }
}