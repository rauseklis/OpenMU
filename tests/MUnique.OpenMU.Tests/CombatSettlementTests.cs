// <copyright file="CombatSettlementTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;
using MonsterAttribute = MUnique.OpenMU.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Follows one attack from the resolved hit through the monster's death to the experience of the player:
/// a hit counts once, a death pays once, and a failing event consumer changes nothing in the game.
/// </summary>
[TestFixture]
public class CombatSettlementTests
{
    private static readonly HitInfo Hundred = new(100, 0, DamageAttributes.Undefined);

    [SetUp]
    public void ResetHealth() => CombatEventHealth.Reset();

    /// <summary>A hit of 100 takes 1,000 HP to 900, also when the same hit is delivered twice.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SingleHitDeliveredTwiceCountsOnceAsync()
    {
        var f = await Fixture.CreateAsync(1000).ConfigureAwait(false);
        var key = new AttackKey(CombatIds.NextActionId(), 0);

        var first = await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, key).ConfigureAwait(false);
        var second = await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, key).ConfigureAwait(false);

        Assert.That(first, Is.True);
        Assert.That(second, Is.False);
        Assert.That(f.Monster.Health, Is.EqualTo(900));
        Assert.That(f.Events(CombatEventKind.HitApplied), Has.Count.EqualTo(1));
        Assert.That(f.Events(CombatEventKind.HitDuplicateIgnored), Has.Count.EqualTo(1));
    }

    /// <summary>A skill with three pulses takes 1,000 HP to 700, also when all pulses are delivered twice.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ThreePulseSkillCountsThreeTimesAsync()
    {
        var f = await Fixture.CreateAsync(1000).ConfigureAwait(false);
        var action = CombatIds.NextActionId();

        for (var round = 0; round < 2; round++)
        {
            for (var pulse = 0; pulse < 3; pulse++)
            {
                await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, new AttackKey(action, pulse)).ConfigureAwait(false);
            }
        }

        Assert.That(f.Monster.Health, Is.EqualTo(700));
        Assert.That(f.Events(CombatEventKind.HitApplied), Has.Count.EqualTo(3));
        Assert.That(f.Events(CombatEventKind.HitDuplicateIgnored), Has.Count.EqualTo(3));
    }

    /// <summary>Two independent attacks which arrive on the same tick both count.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TwoIndependentAttacksOnSameTickBothCountAsync()
    {
        var f = await Fixture.CreateAsync(1000).ConfigureAwait(false);
        var a = new AttackKey(CombatIds.NextActionId(), 0);
        var b = new AttackKey(CombatIds.NextActionId(), 0);

        await RunTogetherAsync(
            () => f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, a).AsTask(),
            () => f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, b).AsTask()).ConfigureAwait(false);

        Assert.That(f.Monster.Health, Is.EqualTo(800));
        Assert.That(f.Events(CombatEventKind.HitApplied), Has.Count.EqualTo(2));
    }

    /// <summary>When several lethal hits arrive at once, the life ends once and the experience is paid once.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ConcurrentLethalHitsEndTheLifeOnceAsync()
    {
        for (var i = 0; i < 200; i++)
        {
            var f = await Fixture.CreateAsync(100).ConfigureAwait(false);
            var attacks = Enumerable.Range(0, 8)
                .Select(_ => new AttackKey(CombatIds.NextActionId(), 0))
                .Select(key => (Func<Task>)(() => f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, key).AsTask()))
                .ToArray();

            await RunTogetherAsync(attacks).ConfigureAwait(false);

            Assert.That(f.Events(CombatEventKind.LifeEnded), Has.Count.EqualTo(1), $"round {i}");
            Assert.That(f.Events(CombatEventKind.ExperienceSettled), Has.Count.EqualTo(1), $"round {i}");
            Assert.That(f.Monster.Health, Is.EqualTo(0));
        }
    }

    /// <summary>A retry of the death handling, also with a re-loaded player, pays nothing more.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RetryAndReloadDoNotPayTwiceAsync()
    {
        var f = await Fixture.CreateAsync(100).ConfigureAwait(false);
        await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, new AttackKey(CombatIds.NextActionId(), 0)).ConfigureAwait(false);
        var afterKill = f.Player.SelectedCharacter!.Experience;
        Assert.That(afterKill, Is.GreaterThan(0));

        // A retry of the settlement of the same life, for example after a failed notification.
        await f.Monster.SettleLifeAsync(f.Player).ConfigureAwait(false);
        Assert.That(f.Player.SelectedCharacter.Experience, Is.EqualTo(afterKill));

        // A relog creates a new Player object for the same character; the life is still the same one.
        var reloaded = await f.CreateReloadedPlayerAsync().ConfigureAwait(false);
        await f.Monster.SettleLifeAsync(reloaded).ConfigureAwait(false);
        Assert.That(reloaded.SelectedCharacter!.Experience, Is.EqualTo(afterKill));
        Assert.That(f.Events(CombatEventKind.ExperienceSettled), Has.Count.EqualTo(1));
    }

    /// <summary>In a party, each eligible member is paid exactly once for one death.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task PartyKillPaysEachMemberOnceAsync()
    {
        var f = await Fixture.CreateAsync(100).ConfigureAwait(false);
        var second = await f.AddPartyMemberAsync().ConfigureAwait(false);

        await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, new AttackKey(CombatIds.NextActionId(), 0)).ConfigureAwait(false);
        await f.Monster.SettleLifeAsync(f.Player).ConfigureAwait(false);

        var settled = f.Events(CombatEventKind.ExperienceSettled);
        Assert.That(settled, Has.Count.EqualTo(2));
        Assert.That(settled.Select(e => e.CharacterId).Distinct().Count(), Is.EqualTo(2));
        Assert.That(settled.Sum(e => e.Experience), Is.GreaterThan(0));
        Assert.That(f.Player.SelectedCharacter!.Experience + second.SelectedCharacter!.Experience, Is.EqualTo(settled.Sum(e => (long)e.Experience)));
    }

    /// <summary>A fresh spawn is a new life with a new entitlement.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task FreshSpawnCreatesNewEntitlementAsync()
    {
        var f = await Fixture.CreateAsync(100).ConfigureAwait(false);
        var firstLife = f.Monster.LifeId;
        await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, new AttackKey(CombatIds.NextActionId(), 0)).ConfigureAwait(false);
        var afterFirst = f.Player.SelectedCharacter!.Experience;

        f.Monster.Initialize();
        await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, new AttackKey(CombatIds.NextActionId(), 0)).ConfigureAwait(false);

        Assert.That(f.Monster.LifeId, Is.Not.EqualTo(firstLife));
        Assert.That(f.Player.SelectedCharacter.Experience, Is.GreaterThan(afterFirst));
        Assert.That(f.Events(CombatEventKind.ExperienceSettled), Has.Count.EqualTo(2));
        Assert.That(f.Events(CombatEventKind.ExperienceSettled).Select(e => e.LifeId).Distinct().Count(), Is.EqualTo(2));
    }

    /// <summary>The settlement is reported as in memory first and as committed only after a successful save.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ExperienceIsCommittedOnlyAfterSaveAsync()
    {
        var f = await Fixture.CreateAsync(100).ConfigureAwait(false);
        await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, new AttackKey(CombatIds.NextActionId(), 0)).ConfigureAwait(false);

        Assert.That(f.Events(CombatEventKind.ExperienceSettled), Has.Count.EqualTo(1));
        Assert.That(f.Events(CombatEventKind.ExperienceCommitted), Is.Empty);

        await f.Player.SaveProgressAsync().ConfigureAwait(false);

        Assert.That(f.Events(CombatEventKind.ExperienceCommitted), Has.Count.EqualTo(1));
        Assert.That(f.Events(CombatEventKind.ExperienceCommitted)[0].LifeId, Is.EqualTo(f.Monster.LifeId));

        await f.Player.SaveProgressAsync().ConfigureAwait(false);
        Assert.That(f.Events(CombatEventKind.ExperienceCommitted), Has.Count.EqualTo(1), "committed once only");
    }

    /// <summary>A consumer which throws changes nothing in the game, and the failure becomes visible.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task FailingConsumerNeitherBlocksNorAddsEffectsAsync()
    {
        var f = await Fixture.CreateAsync(1000, throwingConsumer: true).ConfigureAwait(false);
        var key = new AttackKey(CombatIds.NextActionId(), 0);

        await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, key).ConfigureAwait(false);
        await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, key).ConfigureAwait(false);

        Assert.That(f.Monster.Health, Is.EqualTo(900), "the duplicate is rejected without any consumer");
        Assert.That(CombatEventHealth.IsDegraded, Is.True);
        Assert.That(CombatEventHealth.FailureCount, Is.GreaterThan(0));

        await f.Monster.ApplyResolvedHitAsync(f.Player, new HitInfo(2000, 0, DamageAttributes.Undefined), new AttackKey(CombatIds.NextActionId(), 0)).ConfigureAwait(false);
        var experience = f.Player.SelectedCharacter!.Experience;
        Assert.That(experience, Is.GreaterThan(0), "the kill is still paid once");
        await f.Monster.SettleLifeAsync(f.Player).ConfigureAwait(false);
        Assert.That(f.Player.SelectedCharacter.Experience, Is.EqualTo(experience));
    }

    /// <summary>
    /// Writes the story of one kill as a readable timeline: which attack was accepted, how much damage applied,
    /// who earned experience and when it was committed.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SampleTimelineReadsAsOneStoryAsync()
    {
        var f = await Fixture.CreateAsync(200).ConfigureAwait(false);
        var first = new AttackKey(CombatIds.NextActionId(), 0);
        var second = new AttackKey(CombatIds.NextActionId(), 0);

        await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, first).ConfigureAwait(false);
        await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, first).ConfigureAwait(false);
        await f.Monster.ApplyResolvedHitAsync(f.Player, Hundred, second).ConfigureAwait(false);
        await f.Player.SaveProgressAsync().ConfigureAwait(false);

        var lines = f.AllEvents().Select(e => e.Kind switch
        {
            CombatEventKind.HitApplied => $"attack {e.Key.ActionId}.{e.Key.Pulse} by {e.Controller}: accepted, {e.HealthDamage} damage applied, monster life {e.LifeId} {e.HealthBefore} -> {e.HealthAfter} HP",
            CombatEventKind.HitDuplicateIgnored => $"attack {e.Key.ActionId}.{e.Key.Pulse}: delivered again, ignored, no damage",
            CombatEventKind.LifeEnded => $"monster life {e.LifeId} ended (killing blow by {e.AttackerName})",
            CombatEventKind.ExperienceSettled => $"character earned {e.Experience} XP for life {e.LifeId}: in memory, not yet saved",
            CombatEventKind.ExperienceCommitted => $"character's {e.Experience} XP for life {e.LifeId}: COMMITTED (save succeeded)",
            _ => e.Kind.ToString(),
        }).ToList();

        TestContext.Out.WriteLine(string.Join(Environment.NewLine, lines));
        Assert.That(lines, Has.Count.EqualTo(6));
        Assert.That(lines[0], Does.Contain("200 -> 100"));
        Assert.That(lines[1], Does.Contain("ignored"));
        Assert.That(lines[2], Does.Contain("100 -> 0"));
        Assert.That(lines[3], Does.Contain("ended"));
        Assert.That(lines[4], Does.Contain("not yet saved"));
        Assert.That(lines[5], Does.Contain("COMMITTED"));
    }

    private static async Task RunTogetherAsync(params Func<Task>[] actions)
    {
        using var barrier = new Barrier(actions.Length);
        await Task.WhenAll(actions.Select(a => Task.Run(() =>
        {
            barrier.SignalAndWait();
            return a();
        }))).ConfigureAwait(false);
    }

    [System.Runtime.InteropServices.Guid("5D1C2E7A-3F94-4B68-A0D5-12E8B7C4F963")]
    private sealed class Recorder : ICombatEventPlugIn
    {
        private readonly List<CombatEvent> _events = new();

        public bool Throws { get; init; }

        public List<CombatEvent> Snapshot()
        {
            lock (this._events)
            {
                return this._events.ToList();
            }
        }

        public void OnCombatEvent(CombatEvent combatEvent)
        {
            lock (this._events)
            {
                this._events.Add(combatEvent);
            }

            if (this.Throws)
            {
                throw new InvalidOperationException("the consumer is broken");
            }
        }
    }

    private sealed class Fixture
    {
        private readonly Recorder _recorder;
        private readonly IGameContext _gameContext;

        private Fixture(IGameContext gameContext, Recorder recorder, Player player, Monster monster)
        {
            this._gameContext = gameContext;
            this._recorder = recorder;
            this.Player = player;
            this.Monster = monster;
        }

        public Player Player { get; }

        public Monster Monster { get; }

        public static async Task<Fixture> CreateAsync(int monsterHealth, bool throwingConsumer = false)
        {
            var contextProvider = new InMemoryPersistenceContextProvider();
            var gameConfiguration = contextProvider.CreateNewContext().CreateNew<GameConfiguration>();
            gameConfiguration.RecoveryInterval = int.MaxValue;
            gameConfiguration.MaximumLevel = 400;
            gameConfiguration.MaximumMasterLevel = 200;
            gameConfiguration.ExperienceRate = 1.0f;
            gameConfiguration.MasterExperienceRate = 1.0f;
            var mapDefinition = contextProvider.CreateNewContext().CreateNew<GameMapDefinition>();
            mapDefinition.ExpMultiplier = 1.0f;
            mapDefinition.TerrainData = new byte[ushort.MaxValue + 3];
            gameConfiguration.Maps.Add(mapDefinition);

            var mapInitializer = new MapInitializer(gameConfiguration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
            var gameContext = new GameContext(
                gameConfiguration,
                contextProvider,
                mapInitializer,
                new NullLoggerFactory(),
                new PlugInManager(new List<PlugInConfiguration>(), new NullLoggerFactory(), null, null),
                NullDropGenerator.Instance,
                new ConfigurationChangeMediator());
            mapInitializer.PlugInManager = gameContext.PlugInManager;
            mapInitializer.PathFinderPool = gameContext.PathFinderPool;

            var recorder = new Recorder { Throws = throwingConsumer };
            gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<ICombatEventPlugIn>(recorder);

            var player = await CreatePlayerAsync(gameContext).ConfigureAwait(false);
            var monster = await CreateMonsterAsync(gameContext, monsterHealth).ConfigureAwait(false);
            return new Fixture(gameContext, recorder, player, monster);
        }

        public List<CombatEvent> AllEvents() => this._recorder.Snapshot();

        public List<CombatEvent> Events(CombatEventKind kind) => this._recorder.Snapshot().Where(e => e.Kind == kind).ToList();

        public async Task<Player> AddPartyMemberAsync()
        {
            var second = await CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
            var party = new Party(new PartyManager(5, new NullLogger<Party>()), 5, new NullLogger<Party>());
            await party.AddAsync(this.Player).ConfigureAwait(false);
            await party.AddAsync(second).ConfigureAwait(false);
            await this.Player.AddObserverAsync(second).ConfigureAwait(false);
            return second;
        }

        public async Task<Player> CreateReloadedPlayerAsync()
        {
            var character = this.Player.SelectedCharacter!;
            var reloaded = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
            await reloaded.SetSelectedCharacterAsync(character).ConfigureAwait(false);
            reloaded.Attributes![Stats.Level] = 50;
            reloaded.Attributes.AddElement(new SimpleElement(1.0f, AggregateType.AddRaw), Stats.ExperienceRate);
            reloaded.Attributes.AddElement(new SimpleElement(50, AggregateType.AddRaw), Stats.TotalLevel);
            return reloaded;
        }

        private static async Task<Player> CreatePlayerAsync(IGameContext gameContext)
        {
            var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
            player.SelectedCharacter!.CharacterClass!.IsMasterClass = false;
            player.Attributes![Stats.Level] = 50;
            player.Attributes[Stats.PointsPerLevelUp] = 1;
            player.Attributes.AddElement(new SimpleElement(1.0f, AggregateType.AddRaw), Stats.ExperienceRate);
            player.Attributes.AddElement(new SimpleElement(50, AggregateType.AddRaw), Stats.TotalLevel);
            player.SelectedCharacter.Experience = 0;
            return player;
        }

        private static async Task<Monster> CreateMonsterAsync(IGameContext gameContext, int health)
        {
            var definition = new MonsterDefinition
            {
                Id = Guid.NewGuid(),
                ObjectKind = NpcObjectKind.Monster,
                RespawnDelay = TimeSpan.FromHours(1),
            };
            definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = health });
            definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = 30 });
            var map = await gameContext.GetMapAsync(0).ConfigureAwait(false);
            var spawnArea = new MonsterSpawnArea
            {
                MonsterDefinition = definition,
                GameMap = map!.Definition,
                X1 = 100,
                Y1 = 100,
                X2 = 100,
                Y2 = 100,
                Quantity = 1,
            };

            var monster = new Monster(spawnArea, definition, map, NullDropGenerator.Instance, new Mock<INpcIntelligence>().Object, gameContext.PlugInManager, gameContext.PathFinderPool);
            monster.Initialize();
            return monster;
        }
    }
}
