// <copyright file="SessionEventTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Tests the session events: who entered the world, who disconnected, and when the server took over a character.
/// </summary>
[TestFixture]
public class SessionEventTests
{
    private const string LoginName = "test";
    private const string CharacterName = "testChar";

    private IGameContext _gameContext = null!;
    private Recorder _recorder = null!;

    /// <summary>Creates a game context with a recording consumer.</summary>
    [SetUp]
    public void SetUp()
    {
        this._gameContext = GameContextTestHelper.CreateGameContext();
        this._recorder = new Recorder();
        this._gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<ISessionEventPlugIn>(this._recorder);
        CombatEventHealth.Reset();
    }

    /// <summary>A player who enters the world and disconnects leaves one event for each, with the character.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EnteringTheWorldAndDisconnectingAreReportedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        player.SelectedCharacter!.Name = "Hero";
        player.ClientBuild = "build-abc";

        await player.DisconnectAsync().ConfigureAwait(false);

        var kinds = this._recorder.Events.Select(e => e.Kind).ToList();
        Assert.That(kinds, Does.Contain(SessionEventKind.EnteredWorld));
        var disconnected = this._recorder.Events.Single(e => e.Kind == SessionEventKind.Disconnected);
        Assert.That(disconnected.CharacterName, Is.EqualTo("Hero"));
        Assert.That(disconnected.Build, Is.EqualTo("build-abc"));
        Assert.That(disconnected.Controller, Is.EqualTo(CombatController.Manual));
        Assert.That(disconnected.CharacterId, Is.Not.Null);
    }

    /// <summary>The handover to offline play and its end are reported with the server as controller.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task OfflinePlayStartAndStopAreReportedAsync()
    {
        var manager = new OfflinePlayerManager();
        var realPlayer = await this.CreatePlayerWithPersistedAccountAsync().ConfigureAwait(false);
        realPlayer.TryAddMoney(1_000_000);
        realPlayer.Attributes![Stats.Level] = 100;

        Assert.That(await manager.StartAsync(realPlayer, LoginName).ConfigureAwait(false), Is.True);
        await manager.StopAsync(LoginName).ConfigureAwait(false);

        var offline = this._recorder.Events.Where(e => e.Kind is SessionEventKind.OfflineStarted or SessionEventKind.OfflineStopped).ToList();
        Assert.That(offline.Select(e => e.Kind), Is.EqualTo(new[] { SessionEventKind.OfflineStarted, SessionEventKind.OfflineStopped }));
        Assert.That(offline.All(e => e.Controller == CombatController.OfflineServerOwned), Is.True);
    }

    /// <summary>A consumer which throws never breaks the disconnect, and the failure is visible.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task FailingConsumerNeverBreaksTheSessionAsync()
    {
        this._recorder.Throws = true;
        var player = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);

        Assert.DoesNotThrowAsync(async () => await player.DisconnectAsync().ConfigureAwait(false));

        Assert.That(CombatEventHealth.IsDegraded, Is.True);
    }

    /// <summary>The disconnect of a connection which never logged in (a port probe) is not reported; everything else is.</summary>
    [Test]
    public void ProbesWithoutAnAccountAreNotReported()
    {
        Assert.That(SessionEventPublisher.ShouldPublish(SessionEventKind.Disconnected, false), Is.False);
        Assert.That(SessionEventPublisher.ShouldPublish(SessionEventKind.Disconnected, true), Is.True);
        Assert.That(SessionEventPublisher.ShouldPublish(SessionEventKind.LoggedIn, true), Is.True);
        Assert.That(SessionEventPublisher.ShouldPublish(SessionEventKind.EnteredWorld, true), Is.True);
    }

    /// <summary>The report of a client is recorded for a logged-in player, and only a few times per session.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task IntegrityReportIsRecordedAFewTimesAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);

        var results = Enumerable.Range(0, Player.MaximumIntegrityReports + 2).Select(_ => player.RecordClientIntegrity("build-abc", "00ff")).ToList();

        Assert.That(results.Count(r => r), Is.EqualTo(Player.MaximumIntegrityReports));
        var recorded = this._recorder.Events.Where(e => e.Kind == SessionEventKind.IntegrityReported).ToList();
        Assert.That(recorded, Has.Count.EqualTo(Player.MaximumIntegrityReports));
        Assert.That(recorded[0].Details, Is.EqualTo("build=build-abc;hash=00ff"));
    }

    /// <summary>A connection which is not logged in cannot make the server record a report.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task IntegrityReportOfAnUnauthenticatedPlayerIsIgnoredAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        player.Account = null;

        Assert.That(player.RecordClientIntegrity("build-abc", "00ff"), Is.False);
        Assert.That(this._recorder.Events.Any(e => e.Kind == SessionEventKind.IntegrityReported), Is.False);
    }

    /// <summary>The handler reads the build and the fingerprint from the packet.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task IntegrityReportHandlerReadsThePacketAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        var packet = new byte[50];
        packet[0] = 0xC1;
        packet[1] = 50;
        packet[2] = 0xF9;
        System.Text.Encoding.ASCII.GetBytes("d7f3dc756664f8f6").CopyTo(packet, 2);
        Enumerable.Range(0, 32).ToList().ForEach(i => packet[18 + i] = (byte)i);

        await new MUnique.OpenMU.GameServer.MessageHandler.ClientIntegrityReportHandlerPlugIn().HandlePacketAsync(player, packet).ConfigureAwait(false);

        var recorded = this._recorder.Events.Single(e => e.Kind == SessionEventKind.IntegrityReported);
        Assert.That(recorded.Details, Does.StartWith("build=d7f3dc756664f8f6;hash=000102030405"));
        Assert.That(recorded.Details, Does.EndWith("1c1d1e1f"));
    }

    /// <summary>The journal keeps session events in the same chain.</summary>
    [Test]
    public void JournalKeepsSessionEventsInTheChain()
    {
        var directory = Path.Combine(Path.GetTempPath(), "session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using (var journal = new CombatJournalPlugIn { Configuration = new CombatJournalConfiguration { Enabled = true, Directory = directory } })
            {
                journal.OnSessionEvent(new SessionEvent(SessionEventKind.LoggedIn, DateTime.UtcNow, Guid.NewGuid(), null, string.Empty, "1.2.3.4", "build-abc", CombatController.Manual));
                journal.OnSessionEvent(new SessionEvent(SessionEventKind.Disconnected, DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), "Hero", "1.2.3.4", "build-abc", CombatController.Manual));
            }

            var lines = File.ReadAllLines(Path.Combine(directory, "combat-journal.jsonl")).Where(l => l.Length > 0).Select(l => JsonSerializer.Deserialize<CombatJournal.JournalLine>(l)!).ToList();
            Assert.That(lines.Select(l => l.Kind), Is.EqualTo(new[] { "LoggedIn", "Disconnected" }));
            Assert.That(lines[1].PrevHash, Is.EqualTo(lines[0].Hash));
            Assert.That(lines[0].Json, Does.Contain("\"Build\":\"build-abc\""));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private async ValueTask<Player> CreatePlayerWithPersistedAccountAsync()
    {
        var config = this._gameContext.Configuration;
        using (var ctx = this._gameContext.PersistenceContextProvider.CreateNewPlayerContext(config))
        {
            var account = ctx.CreateNew<MUnique.OpenMU.DataModel.Entities.Account>();
            account.LoginName = LoginName;
            var character = ctx.CreateNew<MUnique.OpenMU.DataModel.Entities.Character>();
            character.Name = CharacterName;
            if (config.CharacterClasses.FirstOrDefault() is { } existingClass)
            {
                character.CharacterClass = existingClass;
            }
            else
            {
                var characterClass = ctx.CreateNew<CharacterClass>();
                characterClass.HomeMap = config.Maps.FirstOrDefault();
                character.CharacterClass = characterClass;
            }

            account.Characters.Add(character);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        var player = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        player.Account!.LoginName = LoginName;
        var mockCharacter = player.SelectedCharacter!;
        mockCharacter.Name = CharacterName;
        mockCharacter.CharacterClass ??= player.Account.UnlockedCharacterClasses.FirstOrDefault();
        if (mockCharacter.CharacterClass is null && config.CharacterClasses.FirstOrDefault() is { } cc)
        {
            mockCharacter.CharacterClass = cc;
        }

        return player;
    }

    [Guid("1D9C5B72-8E04-4A37-B6F1-3A70E2C94D58")]
    private sealed class Recorder : ISessionEventPlugIn
    {
        private readonly List<SessionEvent> _events = new();

        public bool Throws { get; set; }

        public List<SessionEvent> Events
        {
            get
            {
                lock (this._events)
                {
                    return this._events.ToList();
                }
            }
        }

        public void OnSessionEvent(SessionEvent sessionEvent)
        {
            lock (this._events)
            {
                this._events.Add(sessionEvent);
            }

            if (this.Throws)
            {
                throw new InvalidOperationException("the consumer is broken");
            }
        }
    }
}
