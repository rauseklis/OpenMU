// <copyright file="ItemCustodyTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests the item custody events: who gave which item to whom, and that the journal keeps them in its chain.
/// </summary>
[TestFixture]
public class ItemCustodyTests
{
    private IGameContext _gameContext = null!;
    private Recorder _recorder = null!;
    private string _directory = string.Empty;

    /// <summary>Creates a game context with a recording consumer and a temporary folder.</summary>
    [SetUp]
    public void SetUp()
    {
        this._gameContext = GameContextTestHelper.CreateGameContext();
        this._recorder = new Recorder();
        this._gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<ICustodyEventPlugIn>(this._recorder);
        this._directory = Path.Combine(Path.GetTempPath(), "custody-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._directory);
        CombatEventHealth.Reset();
    }

    /// <summary>Deletes the temporary folder.</summary>
    [TearDown]
    public void TearDown() => Directory.Delete(this._directory, true);

    /// <summary>A finished trade reports the giver, the receiver and the item.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TradeReportsGiverReceiverAndItemAsync()
    {
        var (giver, receiver) = await this.CreateTwoPlayersAsync().ConfigureAwait(false);
        var item = NewItem("Sword", 3);

        ((IItemTradedToOtherPlayerPlugIn)new ItemCustodyPlugIn()).ItemTraded(giver, receiver, item);

        var e = this._recorder.Events.Single();
        Assert.That(e.Kind, Is.EqualTo(CustodyEventKind.ItemTraded));
        Assert.That(e.ItemName, Is.EqualTo("Sword"));
        Assert.That(e.ItemLevel, Is.EqualTo(3));
        Assert.That(e.FromName, Is.EqualTo(giver.Name));
        Assert.That(e.ToName, Is.EqualTo(receiver.Name));
        Assert.That(e.FromCharacterId, Is.EqualTo(((MUnique.OpenMU.Persistence.IIdentifiable)giver.SelectedCharacter!).Id));
        Assert.That(e.ToCharacterId, Is.EqualTo(((MUnique.OpenMU.Persistence.IIdentifiable)receiver.SelectedCharacter!).Id));
    }

    /// <summary>A sale through a personal store reports seller and buyer.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task StoreSaleReportsSellerAndBuyerAsync()
    {
        var (seller, buyer) = await this.CreateTwoPlayersAsync().ConfigureAwait(false);

        ((IItemSoldToOtherPlayerPlugIn)new ItemCustodyPlugIn()).ItemSold(seller, NewItem("Shield", 0), buyer);

        var e = this._recorder.Events.Single();
        Assert.That(e.Kind, Is.EqualTo(CustodyEventKind.ItemSoldToPlayer));
        Assert.That(e.FromName, Is.EqualTo(seller.Name));
        Assert.That(e.ToName, Is.EqualTo(buyer.Name));
    }

    /// <summary>Buying from and selling to a merchant, and a monster drop, are reported with the right sides.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task MerchantAndMonsterEventsAreReportedAsync()
    {
        var (player, _) = await this.CreateTwoPlayersAsync().ConfigureAwait(false);
        var merchant = await this.CreateMonsterAsync("Potion Girl", NpcObjectKind.PassiveNpc).ConfigureAwait(false);
        var monster = await this.CreateMonsterAsync("Spider", NpcObjectKind.Monster).ConfigureAwait(false);
        var plugIn = new ItemCustodyPlugIn();

        plugIn.ItemBought(player, NewItem("Potion", 0), NewItem("Potion", 0), merchant);
        ((IItemSoldToMerchantPlugIn)plugIn).ItemSold(player, NewItem("Ring", 1), merchant);
        plugIn.ItemDropping(monster, player, NewItem("Jewel", 0), new CancelEventArgs());

        Assert.That(this._recorder.Events.Select(e => e.Kind), Is.EqualTo(new[] { CustodyEventKind.ItemBoughtFromMerchant, CustodyEventKind.ItemSoldToMerchant, CustodyEventKind.ItemIssuedByMonster }));
        Assert.That(this._recorder.Events[0].FromName, Is.EqualTo("Potion Girl"));
        Assert.That(this._recorder.Events[0].ToName, Is.EqualTo(player.Name));
        Assert.That(this._recorder.Events[1].ToName, Is.EqualTo("Potion Girl"));
        Assert.That(this._recorder.Events[2].FromName, Is.EqualTo("Spider"));
        Assert.That(this._recorder.Events[2].ToName, Is.EqualTo(player.Name));
    }

    /// <summary>A consumer which throws changes nothing and the failure is visible.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task FailingConsumerNeverBreaksTheGameAsync()
    {
        this._recorder.Throws = true;
        var (giver, receiver) = await this.CreateTwoPlayersAsync().ConfigureAwait(false);

        Assert.DoesNotThrow(() => ((IItemTradedToOtherPlayerPlugIn)new ItemCustodyPlugIn()).ItemTraded(giver, receiver, NewItem("Sword", 0)));

        Assert.That(CombatEventHealth.IsDegraded, Is.True);
    }

    /// <summary>The journal writes custody events into the same hash chain as combat events.</summary>
    [Test]
    public void JournalKeepsCustodyAndCombatEventsInOneChain()
    {
        using var journal = new CombatJournalPlugIn { Configuration = new CombatJournalConfiguration { Enabled = true, Directory = this._directory } };

        journal.OnCombatEvent(new CombatEvent(CombatEventKind.HitApplied, DateTime.UtcNow, new AttackKey(1, 0), 1, 2, 3, "Hero", Guid.NewGuid(), CombatController.Manual, 200, 100, 100, 0, 0));
        journal.OnCustodyEvent(new CustodyEvent(CustodyEventKind.ItemTraded, DateTime.UtcNow, Guid.NewGuid(), "Sword", 3, Guid.NewGuid(), "A", Guid.NewGuid(), "B"));
        journal.OnCombatEvent(new CombatEvent(CombatEventKind.LifeEnded, DateTime.UtcNow, new AttackKey(1, 0), 1, 2, 3, "Hero", Guid.NewGuid(), CombatController.Manual, 100, 0, 100, 0, 0));
        journal.Dispose();

        var lines = File.ReadAllLines(Path.Combine(this._directory, "combat-journal.jsonl")).Where(l => l.Length > 0).Select(l => JsonSerializer.Deserialize<CombatJournal.JournalLine>(l)!).ToList();
        Assert.That(lines.Select(l => l.Kind), Is.EqualTo(new[] { "HitApplied", "ItemTraded", "LifeEnded" }));
        var previous = CombatJournal.Genesis;
        foreach (var line in lines)
        {
            Assert.That(line.PrevHash, Is.EqualTo(previous));
            Assert.That(line.Hash, Is.EqualTo(CombatJournal.ComputeHash(previous, line.Sequence, line.UtcTime, line.Kind, line.Json)));
            previous = line.Hash;
        }
    }

    private static Item NewItem(string name, byte level)
    {
        var mock = new Mock<Item>();
        mock.SetupAllProperties();
        mock.As<MUnique.OpenMU.Persistence.IIdentifiable>().SetupGet(i => i.Id).Returns(Guid.NewGuid());
        mock.Object.Definition = new ItemDefinition { Width = 1, Height = 1, Name = new MUnique.OpenMU.Interfaces.LocalizedString(name) };
        mock.Object.Level = level;
        return mock.Object;
    }

    private async Task<(Player First, Player Second)> CreateTwoPlayersAsync()
    {
        var first = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        var second = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        first.SelectedCharacter!.Name = "Giver";
        second.SelectedCharacter!.Name = "Receiver";
        return (first, second);
    }

    private async Task<Monster> CreateMonsterAsync(string designation, NpcObjectKind kind)
    {
        var definition = new MonsterDefinition { Id = Guid.NewGuid(), ObjectKind = kind, Designation = designation };
        var map = await this._gameContext.GetMapAsync(0).ConfigureAwait(false);
        var spawnArea = new MonsterSpawnArea { MonsterDefinition = definition, GameMap = map!.Definition, X1 = 100, Y1 = 100, X2 = 100, Y2 = 100, Quantity = 1 };
        return new Monster(spawnArea, definition, map, NullDropGenerator.Instance, new Mock<INpcIntelligence>().Object, this._gameContext.PlugInManager, this._gameContext.PathFinderPool);
    }

    [Guid("0B7D2F58-3A6C-4E19-8D41-96C5E7A2B03F")]
    private sealed class Recorder : ICustodyEventPlugIn
    {
        private readonly List<CustodyEvent> _events = new();

        public bool Throws { get; set; }

        public List<CustodyEvent> Events
        {
            get
            {
                lock (this._events)
                {
                    return this._events.ToList();
                }
            }
        }

        public void OnCustodyEvent(CustodyEvent custodyEvent)
        {
            lock (this._events)
            {
                this._events.Add(custodyEvent);
            }

            if (this.Throws)
            {
                throw new InvalidOperationException("the consumer is broken");
            }
        }
    }
}
