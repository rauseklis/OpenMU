// <copyright file="ReusableEventTicketTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.PlayerActions.MiniGames;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Checks configurable shop stock, real inventory selection and Kalima use accounting.
/// </summary>
[TestFixture]
public class ReusableEventTicketTests
{
    [Test]
    public async Task ShopPreservesEditedUsesAndPricesAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var shop = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        var tickets = shop.Items.Where(item => item.Definition is { Group: 13, Number: >= 46 and <= 48 }).ToArray();
        Assert.That(tickets, Has.Length.EqualTo(3));
        Assert.That(tickets.All(item => item.Durability == 10 && item.StorePrice == 1000), Is.True);
        int[] uses = [1, 5, 10];
        for (var index = 0; index < tickets.Length; index++)
        {
            tickets[index].Durability = uses[index];
            tickets[index].StorePrice = 5000 + index;
            Assert.That(tickets[index].Definition!.IsRepairable, Is.False);
            Assert.That(tickets[index].Definition!.IsDroppable, Is.False);
        }

        new ReusableEventTicketItems(context, configuration).Initialize();
        new ReusableEventTicketShop(context, configuration).Initialize();
        Assert.That(tickets.Select(item => item.Durability), Is.EqualTo(uses));
        Assert.That(tickets.Select(item => item.StorePrice), Is.EqualTo(new[] { 5000, 5001, 5002 }));
        Assert.That(shop.Items.Select(item => item.ItemSlot).Distinct().Count(), Is.EqualTo(shop.Items.Count));
    }

    [TestCase(MiniGameType.DevilSquare, 46, true)]
    [TestCase(MiniGameType.BloodCastle, 47, true)]
    [TestCase(MiniGameType.DevilSquare, 47, false)]
    [TestCase(MiniGameType.BloodCastle, 46, false)]
    [TestCase(MiniGameType.BloodCastle, 48, false)]
    [TestCase(MiniGameType.ChaosCastle, 47, false)]
    public async Task EntryFindsOnlyTheMatchingReusableTicketAsync(MiniGameType type, short number, bool accepted)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        using var context = player.PersistenceContext;
        var game = context.CreateNew<MiniGameDefinition>();
        game.Type = type;
        game.TicketItem = context.CreateNew<ItemDefinition>();
        game.TicketItem.Group = 14;
        game.TicketItem.Number = 19;
        game.TicketItemLevel = 7;
        var item = context.CreateNew<Item>();
        item.Definition = context.CreateNew<ItemDefinition>();
        item.Definition.Group = 13;
        item.Definition.Number = number;
        item.Definition.Width = item.Definition.Height = 1;
        item.Durability = 5;
        await player.Inventory!.AddItemAsync(20, item).ConfigureAwait(false);
        var action = new EnterMiniGameAction();
        Assert.That(action.CheckTicketItem(game, player, 255, out var selected), Is.EqualTo(accepted));
        if (accepted)
        {
            Assert.That(selected, Is.SameAs(item));
        }

        item.Durability = 0;
        Assert.That(action.CheckTicketItem(game, player, 20, out _), Is.False);
    }

    [TestCase(MiniGameType.DevilSquare, 46, 1)]
    [TestCase(MiniGameType.DevilSquare, 46, 5)]
    [TestCase(MiniGameType.DevilSquare, 46, 10)]
    [TestCase(MiniGameType.BloodCastle, 47, 1)]
    [TestCase(MiniGameType.BloodCastle, 47, 5)]
    [TestCase(MiniGameType.BloodCastle, 47, 10)]
    public async Task EntryConsumesOneUseOnlyAfterSuccessAsync(MiniGameType type, short number, int uses)
    {
        var game = GameContextTestHelper.CreateGameContext();
        var player = await PlayerTestHelper.CreatePlayerAsync(game).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        player.IsAlive = true;
        using var context = game.PersistenceContextProvider.CreateNewContext();
        var definition = CreateEntryDefinition(context, game.Configuration, type);
        var ticket = context.CreateNew<Item>();
        ticket.Definition = context.CreateNew<ItemDefinition>();
        ticket.Definition.Group = 13;
        ticket.Definition.Number = number;
        ticket.Definition.Width = ticket.Definition.Height = 1;
        ticket.Durability = uses;
        await player.Inventory!.AddItemAsync(20, ticket).ConfigureAwait(false);
        var action = new EnterMiniGameAction();
        player.Attributes![Stats.Level] = 50;
        await action.TryEnterMiniGameAsync(player, type, 1, 255).ConfigureAwait(false);
        Assert.That(ticket.Durability, Is.EqualTo(uses), "Rejected entry must not spend a use.");
        player.Attributes[Stats.Level] = 100;
        await action.TryEnterMiniGameAsync(player, type, 1, 255).ConfigureAwait(false);
        Assert.That(player.CurrentMiniGame, Is.Not.Null);
        Assert.That(ticket.Durability, Is.EqualTo(uses - 1));
        Assert.That(player.Inventory.GetItem(20), uses == 1 ? Is.Null : Is.SameAs(ticket));
        await player.CurrentMiniGame!.DisposeAsync().ConfigureAwait(false);
    }

    private static MiniGameDefinition CreateEntryDefinition(MUnique.OpenMU.Persistence.IContext context, GameConfiguration configuration, MiniGameType type)
    {
        var map = context.CreateNew<GameMapDefinition>();
        map.Number = 1;
        map.TerrainData = new byte[ushort.MaxValue + 3];
        configuration.Maps.Add(map);
        var entrance = context.CreateNew<ExitGate>();
        entrance.Map = map;
        var definition = context.CreateNew<MiniGameDefinition>();
        definition.Type = type;
        definition.GameLevel = 1;
        definition.Entrance = entrance;
        definition.MinimumCharacterLevel = 100;
        definition.MaximumCharacterLevel = 400;
        definition.MaximumPlayerCount = 2;
        definition.TicketItem = context.CreateNew<ItemDefinition>();
        definition.TicketItem.Group = type == MiniGameType.BloodCastle ? (byte)13 : (byte)14;
        definition.TicketItem.Number = type == MiniGameType.BloodCastle ? (short)18 : (short)19;
        definition.TicketItemLevel = 1;
        configuration.MiniGameDefinitions.Add(definition);
        return definition;
    }

    [TestCase(39, false, 0)]
    [TestCase(40, false, 1)]
    [TestCase(130, false, 1)]
    [TestCase(131, false, 2)]
    [TestCase(349, false, 6)]
    [TestCase(350, false, 7)]
    [TestCase(19, true, 0)]
    [TestCase(20, true, 1)]
    [TestCase(111, true, 2)]
    [TestCase(350, true, 7)]
    public async Task KalimaSelectsTheHighestEligibleLevelAsync(short level, bool specialClass, byte expected)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.CharacterClass!.LevelWarpRequirementReductionPercent = specialClass ? 50 : 0;
        player.Attributes![Stats.Level] = level;
        Assert.That(KalimaGateCreator.GetLevel(player), Is.EqualTo(expected));
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(10)]
    public async Task KalimaUsesAreConsumedAndLastUseDestroysTicketAsync(int uses)
    {
        var (player, item) = await CreateKalimaFixtureAsync(uses).ConfigureAwait(false);
        GateNpc? created = null;
        player.CurrentMap!.ObjectAdded += args =>
        {
            created = args.Object as GateNpc;
            return ValueTask.CompletedTask;
        };
        var action = new ItemConsumeAction();
        for (var remaining = uses - 1; remaining >= 0; remaining--)
        {
            await action.HandleConsumeRequestAsync(player, 20, 0, FruitUsage.AddPoints).ConfigureAwait(false);
            Assert.That(created, Is.Not.Null, "A successful use must create a portal.");
            Assert.That(item.Durability, Is.EqualTo(remaining));
            Assert.That(player.Inventory!.GetItem(20), remaining == 0 ? Is.Null : Is.SameAs(item));
            await created!.CurrentMap.RemoveAsync(created).ConfigureAwait(false);
            await created.DisposeAsync().ConfigureAwait(false);
            created = null;
        }
    }

    [TestCase("safezone")]
    [TestCase("below-level")]
    [TestCase("inside-kalima")]
    [TestCase("missing-gate")]
    public async Task RejectedKalimaUseKeepsTicketAsync(string reason)
    {
        var (player, item) = await CreateKalimaFixtureAsync(10).ConfigureAwait(false);
        switch (reason)
        {
            case "safezone":
                player.CurrentMap!.Terrain.ApplyTerrainAttribute(player.Position.X, player.Position.Y, TerrainAttributeType.Safezone, true);
                break;
            case "below-level":
                player.Attributes![Stats.Level] = 39;
                break;
            case "inside-kalima":
                player.CurrentMap!.Definition.Number = 24;
                break;
            case "missing-gate":
                player.GameContext.Configuration.Monsters.Clear();
                break;
        }

        await new ItemConsumeAction().HandleConsumeRequestAsync(player, 20, 0, FruitUsage.Undefined).ConfigureAwait(false);
        Assert.That(item.Durability, Is.EqualTo(10));
        Assert.That(player.Inventory!.GetItem(20), Is.SameAs(item));
    }

    private static async Task<(Player Player, Item Item)> CreateKalimaFixtureAsync(int uses)
    {
        var game = GameContextTestHelper.CreateGameContext([new PlugInConfiguration
        {
            TypeId = new Guid("86823C7C-1717-484C-A985-9423D9416043"), IsActive = true,
        }]);
        var player = await PlayerTestHelper.CreatePlayerAsync(game).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        player.IsAlive = true;
        player.Attributes![Stats.Level] = 40;
        using var context = game.PersistenceContextProvider.CreateNewContext();
        var destination = context.CreateNew<GameMapDefinition>();
        destination.Number = 24;
        var exit = context.CreateNew<ExitGate>();
        exit.Map = destination;
        destination.ExitGates.Add(exit);
        game.Configuration.Maps.Add(destination);
        var npc = context.CreateNew<MonsterDefinition>();
        npc.Number = 152;
        npc.ObjectKind = NpcObjectKind.Gate;
        game.Configuration.Monsters.Add(npc);
        var item = context.CreateNew<Item>();
        item.Definition = context.CreateNew<ItemDefinition>();
        item.Definition.Group = 13;
        item.Definition.Number = 48;
        item.Definition.Width = item.Definition.Height = 1;
        item.Durability = uses;
        await player.Inventory!.AddItemAsync(20, item).ConfigureAwait(false);
        return (player, item);
    }
}
