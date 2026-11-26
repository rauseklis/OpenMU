// <copyright file="RequestFilterTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Duel;
using MUnique.OpenMU.GameLogic.PlayerActions.Party;
using MUnique.OpenMU.GameLogic.PlayerActions.Trade;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Duel;
using MUnique.OpenMU.GameLogic.Views.Party;
using MUnique.OpenMU.GameLogic.Views.Trade;
using MUnique.OpenMU.GameServer;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests the request filter: the chat command /re and its effect on party, trade and duel requests.
/// </summary>
[TestFixture]
public class RequestFilterTests
{
    private const string RefusedText = "is not accepting requests";

    /// <summary>
    /// Tests that a new player shows requests by default.
    /// </summary>
    [Test]
    public async ValueTask NewPlayerHasRequestsOnAsync()
    {
        var player = await CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);

        Assert.That(player.RequestMode, Is.EqualTo(RequestMode.On));
    }

    /// <summary>
    /// Tests that the chat command sets the mode, whatever the previous mode was, and ignores the case.
    /// </summary>
    /// <param name="start">The mode before the command.</param>
    /// <param name="command">The chat command.</param>
    /// <param name="expected">The expected mode after the command.</param>
    [TestCase(RequestMode.On, "/re off", RequestMode.Off)]
    [TestCase(RequestMode.On, "/re auto", RequestMode.Auto)]
    [TestCase(RequestMode.Off, "/re on", RequestMode.On)]
    [TestCase(RequestMode.Auto, "/re ON", RequestMode.On)]
    [TestCase(RequestMode.Off, "/re Auto", RequestMode.Auto)]
    public async ValueTask CommandSetsModeAsync(RequestMode start, string command, RequestMode expected)
    {
        var player = await CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        player.RequestMode = start;

        await new RequestModeChatCommandPlugIn().HandleCommandAsync(player, command).ConfigureAwait(false);

        Assert.That(player.RequestMode, Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests that an unknown or missing mode keeps the current mode and tells the player how to use the command.
    /// </summary>
    /// <param name="command">The chat command.</param>
    [TestCase("/re maybe")]
    [TestCase("/re")]
    public async ValueTask CommandWithUnknownModeKeepsModeAsync(string command)
    {
        var player = await CreatePlayerAsync(GameContextTestHelper.CreateGameContext()).ConfigureAwait(false);
        player.RequestMode = RequestMode.Auto;

        await new RequestModeChatCommandPlugIn().HandleCommandAsync(player, command).ConfigureAwait(false);

        Assert.That(player.RequestMode, Is.EqualTo(RequestMode.Auto));
        VerifyBlueMessage(player, "Usage: /re on", Times.Once());
    }

    /// <summary>
    /// Tests that the command has no effect on a server which has the request filter switched off.
    /// </summary>
    [Test]
    public async ValueTask CommandIsIgnoredOnServerWithFilterDisabledAsync()
    {
        var player = await CreatePlayerAsync(CreateGameServerContext(filterDisabled: true)).ConfigureAwait(false);

        await new RequestModeChatCommandPlugIn().HandleCommandAsync(player, "/re off").ConfigureAwait(false);

        Assert.That(player.RequestMode, Is.EqualTo(RequestMode.On));
        VerifyBlueMessage(player, "switched off on this server", Times.Once());
    }

    /// <summary>
    /// Tests that a party request to a player with requests off is refused: the target sees nothing, the requester is told.
    /// </summary>
    [Test]
    public async ValueTask PartyRequestIsRefusedWhenTargetHasRequestsOffAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var target = await CreatePlayerAsync(gameContext, "Target").ConfigureAwait(false);
        target.RequestMode = RequestMode.Off;

        await new PartyRequestAction().HandlePartyRequestAsync(player, target).ConfigureAwait(false);

        Assert.That(player.Party, Is.Null);
        Assert.That(target.Party, Is.Null);
        Assert.That(target.LastPartyRequester, Is.Null);
        Assert.That(target.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        Mock.Get(target.ViewPlugIns.GetPlugIn<IShowPartyRequestPlugIn>()!).Verify(v => v.ShowPartyRequestAsync(player), Times.Never);
        VerifyBlueMessage(player, RefusedText, Times.Once());
        VerifyBlueMessage(target, RefusedText, Times.Never());
    }

    /// <summary>
    /// Tests that a party request to a player with requests on is shown as usual.
    /// </summary>
    [Test]
    public async ValueTask PartyRequestIsShownWhenTargetHasRequestsOnAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var target = await CreatePlayerAsync(gameContext, "Target").ConfigureAwait(false);

        await new PartyRequestAction().HandlePartyRequestAsync(player, target).ConfigureAwait(false);

        Assert.That(target.LastPartyRequester, Is.SameAs(player));
        Mock.Get(target.ViewPlugIns.GetPlugIn<IShowPartyRequestPlugIn>()!).Verify(v => v.ShowPartyRequestAsync(player), Times.Once);
        VerifyBlueMessage(player, RefusedText, Times.Never());
    }

    /// <summary>
    /// Tests that a party request to a player with requests set to auto is accepted without showing the request.
    /// </summary>
    [Test]
    public async ValueTask PartyRequestIsAcceptedWhenTargetHasRequestsAutoAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var target = await CreatePlayerAsync(gameContext, "Target").ConfigureAwait(false);
        target.RequestMode = RequestMode.Auto;

        await new PartyRequestAction().HandlePartyRequestAsync(player, target).ConfigureAwait(false);

        Assert.That(player.Party, Is.Not.Null);
        Assert.That(target.Party, Is.SameAs(player.Party));
        Assert.That(player.Party!.PartyList, Contains.Item(target));
        Assert.That(target.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        Mock.Get(target.ViewPlugIns.GetPlugIn<IShowPartyRequestPlugIn>()!).Verify(v => v.ShowPartyRequestAsync(player), Times.Never);
    }

    /// <summary>
    /// Tests that an idle party master with requests set to auto takes the requester into the party.
    /// </summary>
    [Test]
    public async ValueTask PartyRequestIsAcceptedByIdlePartyMasterWithRequestsAutoAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var master = await CreatePlayerAsync(gameContext, "Master").ConfigureAwait(false);
        var party = gameContext.PartyManager.CreateParty();
        await party.AddAsync(master).ConfigureAwait(false);
        master.RequestMode = RequestMode.Auto;

        await new PartyRequestAction().HandlePartyRequestAsync(player, master).ConfigureAwait(false);

        Assert.That(player.Party, Is.SameAs(party));
        Assert.That(master.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
    }

    /// <summary>
    /// Tests that a party master with requests set to auto, who is busy (for example in an NPC dialog or a trade),
    /// does not accept on its own: his state must stay as it is.
    /// </summary>
    [Test]
    public async ValueTask PartyRequestIsNotAcceptedByBusyPartyMasterWithRequestsAutoAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var master = await CreatePlayerAsync(gameContext, "Master").ConfigureAwait(false);
        var party = gameContext.PartyManager.CreateParty();
        await party.AddAsync(master).ConfigureAwait(false);
        master.RequestMode = RequestMode.Auto;
        await master.PlayerState.TryAdvanceToAsync(PlayerState.NpcDialogOpened).ConfigureAwait(false);

        await new PartyRequestAction().HandlePartyRequestAsync(player, master).ConfigureAwait(false);

        Assert.That(player.Party, Is.Null);
        Assert.That(party.PartyList, Does.Not.Contain(player));
        Assert.That(master.PlayerState.CurrentState, Is.EqualTo(PlayerState.NpcDialogOpened));
    }

    /// <summary>
    /// Tests that a trade request is shown as usual on a server which has the request filter switched off.
    /// </summary>
    [Test]
    public async ValueTask TradeRequestIsShownOnServerWithFilterDisabledAsync()
    {
        var gameContext = CreateGameServerContext(filterDisabled: true);
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var target = await CreatePlayerAsync(gameContext, "Target").ConfigureAwait(false);
        target.RequestMode = RequestMode.Off;

        var success = await new TradeRequestAction().RequestTradeAsync(player, target).ConfigureAwait(false);

        Assert.That(success, Is.True);
        VerifyBlueMessage(player, RefusedText, Times.Never());
    }

    /// <summary>
    /// Tests that a duel request is not refused by the filter on a server which has it switched off.
    /// </summary>
    [Test]
    public async ValueTask DuelRequestIsNotRefusedOnServerWithFilterDisabledAsync()
    {
        var gameContext = CreateGameServerContext(filterDisabled: true);
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var target = await CreatePlayerAsync(gameContext, "Target").ConfigureAwait(false);
        target.RequestMode = RequestMode.Off;

        await new DuelActions().HandleDuelRequestAsync(player, target).ConfigureAwait(false);

        VerifyBlueMessage(player, RefusedText, Times.Never());
    }

    /// <summary>
    /// Tests that a requester who has requests off can still send requests himself.
    /// </summary>
    [Test]
    public async ValueTask RequesterWithRequestsOffCanStillSendRequestsAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var target = await CreatePlayerAsync(gameContext, "Target").ConfigureAwait(false);
        player.RequestMode = RequestMode.Off;

        await new PartyRequestAction().HandlePartyRequestAsync(player, target).ConfigureAwait(false);

        Mock.Get(target.ViewPlugIns.GetPlugIn<IShowPartyRequestPlugIn>()!).Verify(v => v.ShowPartyRequestAsync(player), Times.Once);
    }

    /// <summary>
    /// Tests that the filter does nothing on a server which has it switched off, even if the player's mode is off.
    /// </summary>
    [Test]
    public async ValueTask PartyRequestIsShownOnServerWithFilterDisabledAsync()
    {
        var gameContext = CreateGameServerContext(filterDisabled: true);
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var target = await CreatePlayerAsync(gameContext, "Target").ConfigureAwait(false);
        target.RequestMode = RequestMode.Off;

        await new PartyRequestAction().HandlePartyRequestAsync(player, target).ConfigureAwait(false);

        Mock.Get(target.ViewPlugIns.GetPlugIn<IShowPartyRequestPlugIn>()!).Verify(v => v.ShowPartyRequestAsync(player), Times.Once);
        VerifyBlueMessage(player, RefusedText, Times.Never());
    }

    /// <summary>
    /// Tests that a trade request to a player with requests off is refused: no trade state is entered and the target sees nothing.
    /// </summary>
    [Test]
    public async ValueTask TradeRequestIsRefusedWhenTargetHasRequestsOffAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var target = await CreatePlayerAsync(gameContext, "Target").ConfigureAwait(false);
        target.RequestMode = RequestMode.Off;

        var success = await new TradeRequestAction().RequestTradeAsync(player, target).ConfigureAwait(false);

        Assert.That(success, Is.False);
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        Assert.That(target.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        Assert.That(player.TradingPartner, Is.Null);
        Mock.Get(target.ViewPlugIns.GetPlugIn<IShowTradeRequestPlugIn>()!).Verify(v => v.ShowTradeRequestAsync(player), Times.Never);
        VerifyBlueMessage(player, RefusedText, Times.Once());
    }

    /// <summary>
    /// Tests that a trade request to a player with requests on or auto is shown as usual (auto only accepts party requests).
    /// </summary>
    /// <param name="mode">The mode of the target.</param>
    [TestCase(RequestMode.On)]
    [TestCase(RequestMode.Auto)]
    public async ValueTask TradeRequestIsShownWhenTargetHasRequestsOnOrAutoAsync(RequestMode mode)
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var target = await CreatePlayerAsync(gameContext, "Target").ConfigureAwait(false);
        target.RequestMode = mode;

        var success = await new TradeRequestAction().RequestTradeAsync(player, target).ConfigureAwait(false);

        Assert.That(success, Is.True);
        Assert.That(target.PlayerState.CurrentState, Is.EqualTo(PlayerState.TradeRequested));
        Mock.Get(target.ViewPlugIns.GetPlugIn<IShowTradeRequestPlugIn>()!).Verify(v => v.ShowTradeRequestAsync(player), Times.Once);
    }

    /// <summary>
    /// Tests that a duel request to a player with requests off is refused: no duel room is taken and the target sees nothing.
    /// </summary>
    [Test]
    public async ValueTask DuelRequestIsRefusedWhenTargetHasRequestsOffAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await CreatePlayerAsync(gameContext, "Requester").ConfigureAwait(false);
        var target = await CreatePlayerAsync(gameContext, "Target").ConfigureAwait(false);
        target.RequestMode = RequestMode.Off;

        await new DuelActions().HandleDuelRequestAsync(player, target).ConfigureAwait(false);

        Assert.That(player.DuelRoom, Is.Null);
        Assert.That(target.DuelRoom, Is.Null);
        Mock.Get(target.ViewPlugIns.GetPlugIn<IShowDuelRequestPlugIn>()!).Verify(v => v.ShowDuelRequestAsync(player), Times.Never);
        VerifyBlueMessage(player, RefusedText, Times.Once());
    }

    private static async ValueTask<Player> CreatePlayerAsync(IGameContext gameContext, string name = "Player")
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.SelectedCharacter!.Name = name;
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        return player;
    }

    private static void VerifyBlueMessage(Player player, string textPart, Times times)
    {
        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!).Verify(
            v => v.ShowMessageAsync(It.Is<string>(m => m.Contains(textPart)), MessageType.BlueNormal),
            times);
    }

    private static IGameServerContext CreateGameServerContext(bool filterDisabled)
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var context = contextProvider.CreateNewContext();
        var gameConfiguration = context.CreateNew<MUnique.OpenMU.Persistence.BasicModel.GameConfiguration>();
        gameConfiguration.MaximumPartySize = 5;
        gameConfiguration.RecoveryInterval = int.MaxValue;
        gameConfiguration.MaximumInventoryMoney = int.MaxValue;
        var mapDef = context.CreateNew<MUnique.OpenMU.Persistence.BasicModel.GameMapDefinition>();
        mapDef.Number = 0;
        mapDef.TerrainData = new byte[ushort.MaxValue + 3];
        gameConfiguration.Maps.Add(mapDef);

        var mapInitializer = new MapInitializer(gameConfiguration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);

        var gameServer = new GameServerContext(
            new GameServerDefinition
            {
                GameConfiguration = gameConfiguration,
                ServerConfiguration = new GameServerConfiguration(),
                RequestFilterDisabled = filterDisabled,
            },
            new Mock<IGuildServer>().Object,
            new Mock<IEventPublisher>().Object,
            new Mock<ILoginServer>().Object,
            new Mock<IFriendServer>().Object,
            new InMemoryPersistenceContextProvider(),
            mapInitializer,
            new NullLoggerFactory(),
            new PlugInManager(new List<PlugInConfiguration>(), new NullLoggerFactory(), null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameServer.PlugInManager;
        mapInitializer.PathFinderPool = gameServer.PathFinderPool;
        return gameServer;
    }
}
