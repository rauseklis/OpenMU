// <copyright file="TradeTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.PlayerActions.Trade;
using MUnique.OpenMU.GameLogic.Views.Trade;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests for the trade actions.
/// </summary>
[TestFixture]
public class TradeTest
{
    /// <summary>
    /// Tests the trade request.
    /// </summary>
    [Test]
    public async ValueTask TestTradeRequestAsync()
    {
        var player = this.CreateTrader(PlayerState.EnteredWorld); // The player which will send the trade request
        var tradePartner = this.CreateTrader(PlayerState.EnteredWorld); // The player which will receive the trade request

        var packetHandler = new TradeRequestAction();
        var success = await packetHandler.RequestTradeAsync(player, tradePartner).ConfigureAwait(false);
        Assert.AreEqual(true, success);
        Assert.AreSame(tradePartner, player.TradingPartner);
        Assert.AreSame(player, tradePartner.TradingPartner);
        Assert.AreEqual(PlayerState.TradeRequested, player.PlayerState.CurrentState);
        Assert.AreEqual(PlayerState.TradeRequested, tradePartner.PlayerState.CurrentState);
        Mock.Get(tradePartner.ViewPlugIns.GetPlugIn<IShowTradeRequestPlugIn>()!).Verify(view => view!.ShowTradeRequestAsync(player), Times.Once);
    }

    /// <summary>
    /// Tests the trade response.
    /// </summary>
    [Test]
    public async ValueTask TestTradeResponseAsync()
    {
        var requester = this.CreateTrader(PlayerState.TradeRequested);
        var responder = this.CreateTrader(PlayerState.TradeRequested);
        requester.TradingPartner = responder;
        responder.TradingPartner = requester;
        var responseHandler = new TradeAcceptAction();
        await responseHandler.HandleTradeAcceptAsync(responder, true).ConfigureAwait(false);
        Assert.AreEqual(requester.PlayerState.CurrentState, PlayerState.TradeOpened);
        Assert.AreEqual(responder.PlayerState.CurrentState, PlayerState.TradeOpened);
        Mock.Get(requester.ViewPlugIns.GetPlugIn<IShowTradeRequestAnswerPlugIn>()!).Verify(view => view!.ShowTradeRequestAnswerAsync(true), Times.Once);
    }

    /// <summary>
    /// Tests the cancellation of a trade.
    /// </summary>
    [Test]
    public async ValueTask TradeCancelTestAsync()
    {
        var trader1 = this.CreateTrader(PlayerState.TradeOpened);
        var trader2 = this.CreateTrader(PlayerState.TradeOpened);
        trader1.TradingPartner = trader2;
        trader2.TradingPartner = trader1;
        var cancelTrader = new TradeCancelAction();
        await cancelTrader.CancelTradeAsync(trader1).ConfigureAwait(false);
        Assert.AreEqual(PlayerState.EnteredWorld, trader1.PlayerState.CurrentState);
        Assert.AreEqual(PlayerState.EnteredWorld, trader2.PlayerState.CurrentState);

        Mock.Get(trader1.ViewPlugIns.GetPlugIn<ITradeFinishedPlugIn>()!).Verify(view => view!.TradeFinishedAsync(TradeResult.Cancelled), Times.Once);
        Mock.Get(trader2.ViewPlugIns.GetPlugIn<ITradeFinishedPlugIn>()!).Verify(view => view!.TradeFinishedAsync(TradeResult.Cancelled), Times.Once);
    }

    /// <summary>
    /// Tests the finishing of a trade.
    /// </summary>
    [Test]
    public async ValueTask TradeFinishTestAsync()
    {
        var trader1 = this.CreateTrader(PlayerState.TradeOpened);
        var trader2 = this.CreateTrader(PlayerState.TradeOpened);
        trader1.TradingPartner = trader2;
        trader2.TradingPartner = trader1;

        var gameContext = new Mock<IGameContext>();
        gameContext.Setup(c => c.PlugInManager).Returns(new PlugInManager(null, new NullLoggerFactory(), null, null));
        gameContext.Setup(c => c.PersistenceContextProvider).Returns(new InMemoryPersistenceContextProvider());

        Mock.Get(trader1).Setup(m => m.GameContext).Returns(gameContext.Object);
        Mock.Get(trader2).Setup(m => m.GameContext).Returns(gameContext.Object);

        var tradeButtonHandler = new TradeButtonAction();
        await tradeButtonHandler.TradeButtonChangedAsync(trader1, TradeButtonState.Unchecked).ConfigureAwait(false);
        Assert.AreEqual(trader1.PlayerState.CurrentState, PlayerState.TradeOpened);
        await tradeButtonHandler.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        Assert.AreEqual(trader1.PlayerState.CurrentState, PlayerState.TradeButtonPressed);
        Assert.AreEqual(trader2.PlayerState.CurrentState, PlayerState.TradeOpened);
        await tradeButtonHandler.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);
        Assert.AreEqual(trader1.PlayerState.CurrentState, PlayerState.EnteredWorld);
        Assert.AreEqual(trader2.PlayerState.CurrentState, PlayerState.EnteredWorld);
        Mock.Get(trader1.ViewPlugIns.GetPlugIn<ITradeFinishedPlugIn>()!).Verify(view => view!.TradeFinishedAsync(TradeResult.Success), Times.Once);
        Mock.Get(trader2.ViewPlugIns.GetPlugIn<ITradeFinishedPlugIn>()!).Verify(view => view!.TradeFinishedAsync(TradeResult.Success), Times.Once);
    }

    /// <summary>
    /// Tests a trade of items.
    /// </summary>
    [Test]
    public async ValueTask TradeItemsAsync()
    {
        var trader1 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var trader2 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var tradeRequestAction = new TradeRequestAction();
        var tradeResponseAction = new TradeAcceptAction();

        var item1 = this.GetItem();
        var item2 = this.GetItem();
        await trader1.Inventory!.AddItemAsync(20, item1).ConfigureAwait(false);
        await trader1.Inventory.AddItemAsync(21, item2).ConfigureAwait(false);
        await tradeRequestAction.RequestTradeAsync(trader1, trader2).ConfigureAwait(false);
        await tradeResponseAction.HandleTradeAcceptAsync(trader2, true).ConfigureAwait(false);
        var itemMoveAction = new MoveItemAction();
        await itemMoveAction.MoveItemAsync(trader1, 20, Storages.Inventory, 0, Storages.Trade).ConfigureAwait(false);
        await itemMoveAction.MoveItemAsync(trader1, 21, Storages.Inventory, 2, Storages.Trade).ConfigureAwait(false);
        Assert.That(trader1.TemporaryStorage!.Items.First(), Is.SameAs(item1));

        var tradeButtonHandler = new TradeButtonAction();
        await tradeButtonHandler.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        await tradeButtonHandler.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);
        Assert.That(trader1.Inventory.ItemStorage.Items, Is.Empty);
        Assert.That(trader2.Inventory!.ItemStorage.Items.First(), Is.SameAs(item1));
    }

    /// <summary>
    /// Tests that the offered money is returned when a trade is cancelled.
    /// </summary>
    [Test]
    public async ValueTask TradeCancelRestoresOfferedMoneyAsync()
    {
        var trader1 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var trader2 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        trader1.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        trader2.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        trader1.Money = 1000;
        trader2.Money = 500;
        await new TradeRequestAction().RequestTradeAsync(trader1, trader2).ConfigureAwait(false);
        await new TradeAcceptAction().HandleTradeAcceptAsync(trader2, true).ConfigureAwait(false);

        var moneyAction = new TradeMoneyAction();
        await moneyAction.TradeMoneyAsync(trader1, 300).ConfigureAwait(false);
        await moneyAction.TradeMoneyAsync(trader1, 700).ConfigureAwait(false);
        await moneyAction.TradeMoneyAsync(trader2, 200).ConfigureAwait(false);
        Assert.That(trader1.Money, Is.EqualTo(300));
        Assert.That(trader2.Money, Is.EqualTo(300));

        await new TradeCancelAction().CancelTradeAsync(trader1).ConfigureAwait(false);

        Assert.That(trader1.Money, Is.EqualTo(1000));
        Assert.That(trader2.Money, Is.EqualTo(500));
        Assert.That(trader1.TradingMoney, Is.EqualTo(0));
        Assert.That(trader2.TradingMoney, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that a finished trade pays the money out exactly once.
    /// </summary>
    [Test]
    public async ValueTask TradeFinishPaysMoneyOnceAsync()
    {
        var trader1 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var trader2 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        trader1.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        trader2.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        trader1.Money = 1000;
        trader2.Money = 500;
        await new TradeRequestAction().RequestTradeAsync(trader1, trader2).ConfigureAwait(false);
        await new TradeAcceptAction().HandleTradeAcceptAsync(trader2, true).ConfigureAwait(false);
        await new TradeMoneyAction().TradeMoneyAsync(trader1, 300).ConfigureAwait(false);

        var tradeButtonHandler = new TradeButtonAction();
        await tradeButtonHandler.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        await tradeButtonHandler.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);
        Assert.That(trader1.Money, Is.EqualTo(700));
        Assert.That(trader2.Money, Is.EqualTo(800));

        // A late cancel on a finished trade must not hand the money out again.
        await new TradeCancelAction().CancelTradeAsync(trader1).ConfigureAwait(false);
        Assert.That(trader1.Money, Is.EqualTo(700));
        Assert.That(trader2.Money, Is.EqualTo(800));
    }

    /// <summary>
    /// Tests a trade of items, when it failes due to missing inventory space.
    /// </summary>
    [Test]
    public async ValueTask TradeFailedItemsNotFitAsync()
    {
        var trader1 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var trader2 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var tradeRequestAction = new TradeRequestAction();
        var tradeResponseAction = new TradeAcceptAction();

        // Fill up inventory of the receiving player
        for (byte i = (byte)(InventoryConstants.LastEquippableItemSlotIndex + 1); i < 64 + InventoryConstants.LastEquippableItemSlotIndex; i++)
        {
            var item = this.GetItem();
            await trader2.Inventory!.AddItemAsync(i, item).ConfigureAwait(false);
        }

        // Create items which should be traded.
        var item1 = this.GetItem();
        var item2 = this.GetItem();
        await trader1.Inventory!.AddItemAsync(20, item1).ConfigureAwait(false);
        await trader1.Inventory.AddItemAsync(21, item2).ConfigureAwait(false);

        // Set up the trade
        await tradeRequestAction.RequestTradeAsync(trader1, trader2).ConfigureAwait(false);
        await tradeResponseAction.HandleTradeAcceptAsync(trader2, true).ConfigureAwait(false);
        var itemMoveAction = new MoveItemAction();
        await itemMoveAction.MoveItemAsync(trader1, 20, Storages.Inventory, 0, Storages.Trade).ConfigureAwait(false);
        await itemMoveAction.MoveItemAsync(trader1, 21, Storages.Inventory, 2, Storages.Trade).ConfigureAwait(false);
        Assert.That(trader1.TemporaryStorage!.Items.First(), Is.SameAs(item1));

        // Accept the trade on both ends
        var tradeButtonHandler = new TradeButtonAction();
        await tradeButtonHandler.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        await tradeButtonHandler.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);

        // Check result
        Assert.That(trader1.Inventory.ItemStorage.Items, Is.Not.Empty);
        Assert.That(trader1.Inventory!.ItemStorage.Items.First(), Is.SameAs(item1));
        Assert.That(trader1.Inventory!.ItemStorage.Items.Last(), Is.SameAs(item2));
    }

    /// <summary>
    /// Tests that a trade cancellation does not abort restoring all valid items when one backup item is invalid.
    /// </summary>
    [Test]
    public async ValueTask TradeCancelIgnoresInvalidBackupItemsAsync()
    {
        var trader1 = this.CreateTrader(PlayerState.TradeOpened);
        var trader2 = this.CreateTrader(PlayerState.TradeOpened);
        Mock.Get(trader1).Setup(t => t.Logger).Returns(NullLogger.Instance);
        Mock.Get(trader2).Setup(t => t.Logger).Returns(NullLogger.Instance);
        trader1.TradingPartner = trader2;
        trader2.TradingPartner = trader1;

        var brokenItem = new Mock<Item>();
        brokenItem.SetupAllProperties();
        brokenItem.Object.ItemSlot = 20;

        var validItem = new Mock<Item>();
        validItem.SetupAllProperties();
        validItem.Object.ItemSlot = 21;

        trader1.BackupInventory = new BackupItemStorage(trader1.Inventory!.ItemStorage)
        {
            Items = new List<Item> { brokenItem.Object, validItem.Object },
        };

        var inventoryMock = Mock.Get(trader1.Inventory!);
        inventoryMock.Setup(i => i.AddItemAsync(20, brokenItem.Object)).Throws(new InvalidOperationException("broken test item"));
        inventoryMock.Setup(i => i.AddItemAsync(21, validItem.Object)).Returns(new ValueTask<bool>(true));

        var cancelAction = new TradeCancelAction();
        await cancelAction.CancelTradeAsync(trader1).ConfigureAwait(false);

        Assert.That(trader1.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        Assert.That(trader2.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        inventoryMock.Verify(i => i.AddItemAsync(21, validItem.Object), Times.Once);
        Assert.That(trader1.BackupInventory, Is.Null);
    }

    private Item GetItem()
    {
        var item = new Mock<Item>();
        item.SetupAllProperties();
        item.Object.Definition = new ItemDefinition { Width = 1, Height = 1 };
        item.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        return item.Object;
    }

    /// <summary>
    /// Tests that a trade which would push the zen of the receiver over the limit (and over the range of the
    /// number) does not finish: nobody loses zen, the receiver's balance never wraps around.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask TradeThatWouldOverflowReceiverZenIsCancelledAsync()
    {
        var trader1 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var trader2 = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        trader1.GameContext.Configuration.MaximumInventoryMoney = 2_000_000_000;
        trader2.GameContext.Configuration.MaximumInventoryMoney = 2_000_000_000;
        trader1.Money = 400_000_000;
        trader2.Money = 1_900_000_000;
        await new TradeRequestAction().RequestTradeAsync(trader1, trader2).ConfigureAwait(false);
        await new TradeAcceptAction().HandleTradeAcceptAsync(trader2, true).ConfigureAwait(false);
        await new TradeMoneyAction().TradeMoneyAsync(trader1, 300_000_000).ConfigureAwait(false);

        var tradeButtonHandler = new TradeButtonAction();
        await tradeButtonHandler.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        await tradeButtonHandler.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);

        Assert.That(trader2.Money, Is.EqualTo(1_900_000_000), "the receiver keeps exactly what they had");
        Assert.That(trader1.Money, Is.EqualTo(400_000_000), "the sender gets the offered zen back");
        Assert.That(trader1.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        Assert.That(trader2.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
    }

    /// <summary>
    /// Tests that both traders are saved right after a finished trade, with the money already moved, so that the
    /// items (which are stored separately) and the payment do not drift apart if the server stops soon afterwards.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask TradeFinishSavesBothTradersAfterMoneyMovedAsync()
    {
        var trader1 = this.CreateTrader(PlayerState.TradeOpened);
        var trader2 = this.CreateTrader(PlayerState.TradeOpened);
        trader1.TradingPartner = trader2;
        trader2.TradingPartner = trader1;
        var gameContext = new Mock<IGameContext>();
        gameContext.Setup(c => c.PlugInManager).Returns(new PlugInManager(null, new NullLoggerFactory(), null, null));
        gameContext.Setup(c => c.PersistenceContextProvider).Returns(new InMemoryPersistenceContextProvider());
        Mock.Get(trader1).Setup(m => m.GameContext).Returns(gameContext.Object);
        Mock.Get(trader2).Setup(m => m.GameContext).Returns(gameContext.Object);

        // trader1 offered 300 of its 1,000 (already taken from its balance); trader2 offers nothing.
        trader1.Money = 700;
        trader1.TradingMoney = 300;
        trader2.Money = 500;
        var savedMoney = new List<(ITrader Trader, int Money)>();
        foreach (var trader in new[] { trader1, trader2 })
        {
            var captured = trader;
            Mock.Get(captured).Setup(t => t.SaveProgressAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    savedMoney.Add((captured, captured.Money));
                    return ValueTask.FromResult(true);
                });
        }

        var tradeButtonHandler = new TradeButtonAction();
        await tradeButtonHandler.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        await tradeButtonHandler.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);

        Assert.That(savedMoney.Any(s => s.Trader == trader1 && s.Money == 700), Is.True, "trader1 saved after the trade");
        Assert.That(savedMoney.Any(s => s.Trader == trader2 && s.Money == 800), Is.True, "trader2 saved with the received money");
    }

    /// <summary>
    /// Tests that a save which fails right after a finished trade does not turn the finished trade into a cancelled one.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask FailingSaveAfterTradeDoesNotUndoTheTradeAsync()
    {
        var trader1 = this.CreateTrader(PlayerState.TradeOpened);
        var trader2 = this.CreateTrader(PlayerState.TradeOpened);
        trader1.TradingPartner = trader2;
        trader2.TradingPartner = trader1;
        var gameContext = new Mock<IGameContext>();
        gameContext.Setup(c => c.PlugInManager).Returns(new PlugInManager(null, new NullLoggerFactory(), null, null));
        gameContext.Setup(c => c.PersistenceContextProvider).Returns(new InMemoryPersistenceContextProvider());
        Mock.Get(trader1).Setup(m => m.GameContext).Returns(gameContext.Object);
        Mock.Get(trader2).Setup(m => m.GameContext).Returns(gameContext.Object);
        trader1.Money = 700;
        trader1.TradingMoney = 300;
        trader2.Money = 500;
        Mock.Get(trader1).Setup(t => t.SaveProgressAsync(It.IsAny<CancellationToken>())).Throws(new InvalidOperationException("database is down"));
        Mock.Get(trader2).Setup(t => t.SaveProgressAsync(It.IsAny<CancellationToken>())).Returns(ValueTask.FromResult(false));

        var tradeButtonHandler = new TradeButtonAction();
        await tradeButtonHandler.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        await tradeButtonHandler.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);

        Mock.Get(trader1.ViewPlugIns.GetPlugIn<ITradeFinishedPlugIn>()!).Verify(view => view!.TradeFinishedAsync(TradeResult.Success), Times.Once);
        Assert.That(trader2.Money, Is.EqualTo(800));
    }

    private ITrader CreateTrader(State playerState)
    {
        var trader = new Mock<ITrader>();
        trader.SetupAllProperties();
        trader.Setup(t => t.PlayerState).Returns(new StateMachine(playerState));
        var inventory = new Mock<IInventoryStorage>();
        var itemStorage = new Mock<ItemStorage>();
        itemStorage.Setup(i => i.Items).Returns(new List<Item>());
        inventory.Setup(i => i.ItemStorage).Returns(itemStorage.Object);
        trader.Setup(t => t.Inventory).Returns(inventory.Object);
        trader.Object.BackupInventory = new BackupItemStorage(itemStorage.Object) { Items = new List<Item>() };
        var temporaryStorage = new Mock<IStorage>();
        temporaryStorage.Setup(t => t.Items).Returns(new List<Item>());
        trader.Setup(t => t.TemporaryStorage).Returns(temporaryStorage.Object);
        trader.Setup(t => t.ViewPlugIns).Returns(new MockViewPlugInContainer());

        var contextMock = new Mock<IPlayerContext>();
        trader.Setup(t => t.PersistenceContext).Returns(contextMock.Object);
        return trader.Object;
    }

    //// TODO: Test fail scenarios
}
