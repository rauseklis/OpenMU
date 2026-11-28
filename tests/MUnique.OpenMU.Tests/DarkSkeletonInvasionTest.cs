// <copyright file="DarkSkeletonInvasionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Runtime.InteropServices;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests the <see cref="DarkSkeletonInvasionPlugIn"/>.
/// </summary>
[TestFixture]
public class DarkSkeletonInvasionTest
{
    private const ushort TestMap = 0;

    private const ushort TownMap = 3;

    private const ushort ExtraMap = 25;

    private const ushort OtherExtraMap = 26;

    private const int TownX1 = 100;

    private const int TownY1 = 100;

    private const int TownX2 = 110;

    private const int TownY2 = 110;

    private const byte GateX1 = 10;

    private const byte GateY1 = 16;

    private const byte GateX2 = 17;

    private const byte GateY2 = 22;

    private const int WallY = 200;

    /// <summary>
    /// The default configuration matches the owner's request: 20:00, 15 minutes, Devias,
    /// 10 Dark Mages, 10 Dark Skeletons and 2 Dark Reapers, ends when the reapers die, on servers 0 and 1
    /// (on server 1 with half strength).
    /// </summary>
    [Test]
    public void DefaultConfiguration()
    {
        var config = DarkSkeletonInvasionConfiguration.Default;

        Assert.That(config.TaskDuration, Is.EqualTo(TimeSpan.FromMinutes(15)));
        Assert.That(config.Timetable, Is.EqualTo(new[] { new TimeOnly(20, 0) }));
        Assert.That(config.GameServerIds, Is.EqualTo(new byte[] { 0, 1 }));
        Assert.That(config.WeakerGameServerIds, Is.EqualTo(new byte[] { 1 }));
        Assert.That(config.WeakerStrengthPercentage, Is.EqualTo(50f));
        Assert.That(config.EndWhenKilledMonsterIds, Is.EqualTo(new ushort[] { InvasionMonsters.DarkReaper }));

        var mages = config.Mobs.Single(m => m.MonsterId == 1113);
        Assert.That(mages.Count, Is.EqualTo(10));
        Assert.That(mages.MapIds, Is.EqualTo(new ushort[] { InvasionMaps.Devias }));
        Assert.That(mages.IsSpawnOnAllMaps, Is.True);

        var skeletons = config.Mobs.Single(m => m.MonsterId == 1111);
        Assert.That(skeletons.Count, Is.EqualTo(10));
        Assert.That(skeletons.MapIds, Is.EqualTo(new ushort[] { InvasionMaps.Devias }));
        Assert.That(skeletons.IsSpawnOnAllMaps, Is.True);

        var reapers = config.Mobs.Single(m => m.MonsterId == 1112);
        Assert.That(reapers.Count, Is.EqualTo(2));
        Assert.That(reapers.MapIds, Is.EqualTo(new ushort[] { InvasionMaps.Devias }));
        Assert.That(reapers.IsSpawnOnAllMaps, Is.True);
        Assert.That(reapers.AnnounceDeath, Is.True);

        Assert.That(config.ExtraMobs, Has.Count.EqualTo(3));
        AssertExtraMob(config, 1113, 5, InvasionMaps.Kalima2, InvasionMaps.Kalima3);
        AssertExtraMob(config, 1111, 4, InvasionMaps.Kalima4, InvasionMaps.Kalima5);
        AssertExtraMob(config, 1112, 3, InvasionMaps.Kalima6, InvasionMaps.Kalima7);
        Assert.That(config.TownDistanceMapIds, Is.EqualTo(new ushort[] { InvasionMaps.Lorencia, InvasionMaps.Noria, InvasionMaps.Elbeland }));
        Assert.That(config.MinimumTownDistance, Is.EqualTo(20));
        Assert.That(config.MinimumEntranceDistance, Is.EqualTo(10));
        Assert.That((string)config.AllBossesKilledMessage, Does.Contain("All Dark Reapers have been defeated! The light returns."));

        static void AssertExtraMob(DarkSkeletonInvasionConfiguration config, ushort monsterId, int count, params ushort[] mapIds)
        {
            var mob = config.ExtraMobs.Single(m => m.MonsterId == monsterId);
            Assert.That(mob.Count, Is.EqualTo(count));
            Assert.That(mob.MapIds, Is.EqualTo(mapIds));
            Assert.That(mob.IsSpawnOnAllMaps, Is.True, "The count is per map.");
            Assert.That(mob.AnnounceDeath, Is.False);
        }
    }

    /// <summary>
    /// The event runs only on the configured game servers (by default servers 0 and 1).
    /// </summary>
    [Test]
    public void RunsOnlyOnConfiguredServers()
    {
        var plugIn = new DarkSkeletonInvasionPlugIn { Configuration = DarkSkeletonInvasionConfiguration.Default };

        Assert.That(plugIn.IsEnabledOn(CreateServerContext(0)), Is.True);
        Assert.That(plugIn.IsEnabledOn(CreateServerContext(1)), Is.True);
        Assert.That(plugIn.IsEnabledOn(CreateServerContext(2)), Is.False);

        plugIn.Configuration.GameServerIds.Remove(1);
        Assert.That(plugIn.IsEnabledOn(CreateServerContext(1)), Is.False);

        plugIn.Configuration.GameServerIds.Clear();
        Assert.That(plugIn.IsEnabledOn(CreateServerContext(1)), Is.True, "An empty list means all servers.");
    }

    /// <summary>
    /// A game master start spawns the monsters; killing one reaper keeps the event running,
    /// killing the last one ends it and removes the remaining skeletons.
    /// </summary>
    [Test]
    public async ValueTask EndsWhenAllReapersAreKilledAsync()
    {
        var (gameContext, plugIn) = CreateContextAndPlugIn();
        try
        {
            var map = (await gameContext.GetMapAsync(TestMap).ConfigureAwait(false))!;

            plugIn.StartByGameMaster(gameContext, TestMap);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false); // prepare
            Assert.That(plugIn.IsRunning(gameContext), Is.True);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false); // start + spawn

            var monsters = map.GetAttackablesInRange(new Pathfinding.Point(128, 128), 255).OfType<Monster>().ToList();
            Assert.That(monsters.Count(m => m.Definition.Number == 1113), Is.EqualTo(10));
            Assert.That(monsters.Count(m => m.Definition.Number == 1111), Is.EqualTo(10));
            var reapers = monsters.Where(m => m.Definition.Number == 1112).ToList();
            Assert.That(reapers, Has.Count.EqualTo(2));

            var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
            await KillAsync(reapers[0], killer).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            Assert.That(plugIn.IsRunning(gameContext), Is.True, "One reaper is still alive.");

            await KillAsync(reapers[1], killer).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            Assert.That(plugIn.IsRunning(gameContext), Is.False, "Both reapers are dead, so the event ends.");

            var remaining = map.GetAttackablesInRange(new Pathfinding.Point(128, 128), 255).OfType<Monster>()
                .Count(m => m.Definition.Number is 1111 or 1113 && m.IsAlive);
            Assert.That(remaining, Is.Zero, "The remaining skeletons are removed when the event ends.");

            // A game master may start it again right away.
            plugIn.StartByGameMaster(gameContext, TestMap);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            Assert.That(plugIn.IsRunning(gameContext), Is.True);
        }
        finally
        {
            await gameContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Without killing the reapers the event keeps running until its duration is over.
    /// </summary>
    [Test]
    public async ValueTask KeepsRunningWhileReapersLiveAsync()
    {
        var (gameContext, plugIn) = CreateContextAndPlugIn();
        try
        {
            plugIn.StartByGameMaster(gameContext, TestMap);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);

            Assert.That(plugIn.IsRunning(gameContext), Is.True);
        }
        finally
        {
            await gameContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// On a server of <see cref="DarkSkeletonInvasionConfiguration.WeakerGameServerIds"/> the monsters
    /// have half of their values (health, damage, defense and rates); on other servers they are unchanged.
    /// </summary>
    /// <param name="serverId">The server id of the map.</param>
    /// <param name="expectedFactor">The expected factor of the values.</param>
    [TestCase((byte)0, 1f)]
    [TestCase((byte)1, 0.5f)]
    public async ValueTask MonsterStrengthDependsOnServerAsync(byte serverId, float expectedFactor)
    {
        var (gameContext, plugIn) = CreateContextAndPlugIn();
        try
        {
            var map = (await gameContext.GetMapAsync(TestMap).ConfigureAwait(false))!;
            map.ServerId = serverId;

            plugIn.StartByGameMaster(gameContext, TestMap);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);

            var monsters = map.GetAttackablesInRange(new Pathfinding.Point(128, 128), 255).OfType<Monster>().ToList();
            Assert.That(monsters, Has.Count.EqualTo(22));
            foreach (var monster in monsters)
            {
                Assert.That(monster.Attributes[GameLogic.Attributes.Stats.MaximumHealth], Is.EqualTo(1000 * expectedFactor).Within(0.01f));
                Assert.That(monster.Health, Is.EqualTo((int)(1000 * expectedFactor)), "The monster starts with its full (changed) health.");
                Assert.That(monster.Attributes[GameLogic.Attributes.Stats.MaximumPhysBaseDmg], Is.EqualTo(200 * expectedFactor).Within(0.01f));
                Assert.That(monster.Attributes[GameLogic.Attributes.Stats.DefenseBase], Is.EqualTo(100 * expectedFactor).Within(0.01f));
                Assert.That(monster.Attributes[GameLogic.Attributes.Stats.AttackRatePvm], Is.EqualTo(400 * expectedFactor).Within(0.01f));
            }
        }
        finally
        {
            await gameContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A game master stop ends a running event on the next check and removes its monsters;
    /// a stop when nothing runs does nothing.
    /// </summary>
    [Test]
    public async ValueTask GameMasterStopEndsRunningEventAsync()
    {
        var (gameContext, plugIn) = CreateContextAndPlugIn();
        try
        {
            var map = (await gameContext.GetMapAsync(TestMap).ConfigureAwait(false))!;
            Assert.That(plugIn.StopByGameMaster(gameContext), Is.False, "Nothing runs yet.");

            plugIn.StartByGameMaster(gameContext, TestMap);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false); // prepare
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false); // start + spawn
            Assert.That(CountLivingEventMonsters(map), Is.EqualTo(22));

            Assert.That(plugIn.StopByGameMaster(gameContext), Is.True);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);

            Assert.That(plugIn.IsRunning(gameContext), Is.False);
            Assert.That(CountLivingEventMonsters(map), Is.Zero);
        }
        finally
        {
            await gameContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A game master stop during the announcement ends the event without spawning monsters.
    /// </summary>
    [Test]
    public async ValueTask GameMasterStopDuringAnnouncementSpawnsNothingAsync()
    {
        var (gameContext, plugIn) = CreateContextAndPlugIn();
        try
        {
            var map = (await gameContext.GetMapAsync(TestMap).ConfigureAwait(false))!;

            plugIn.StartByGameMaster(gameContext, TestMap);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false); // prepare
            Assert.That(plugIn.StopByGameMaster(gameContext), Is.True);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);

            Assert.That(plugIn.IsRunning(gameContext), Is.False);
            Assert.That(CountLivingEventMonsters(map), Is.Zero);

            // It can be started again normally afterwards.
            plugIn.StartByGameMaster(gameContext, TestMap);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            Assert.That(CountLivingEventMonsters(map), Is.EqualTo(22));
        }
        finally
        {
            await gameContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A game master start begins the event only on the game server where it was requested;
    /// another game server which shares the plug-in keeps running without it.
    /// </summary>
    [Test]
    public async ValueTask GameMasterStartOnlyAffectsItsServerAsync()
    {
        var (gameContext, plugIn) = CreateContextAndPlugIn();
        var (otherContext, _) = CreateContextAndPlugIn();
        try
        {
            var otherMap = (await otherContext.GetMapAsync(TestMap).ConfigureAwait(false))!;

            plugIn.StartByGameMaster(gameContext, TestMap);
            await plugIn.ExecuteTaskAsync(otherContext).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false); // prepare
            await plugIn.ExecuteTaskAsync(otherContext).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false); // start + spawn
            await plugIn.ExecuteTaskAsync(otherContext).ConfigureAwait(false);

            Assert.That(plugIn.IsRunning(gameContext), Is.True);
            Assert.That(plugIn.IsRunning(otherContext), Is.False, "The other server doesn't start the event.");
            Assert.That(CountLivingEventMonsters(otherMap), Is.Zero);
        }
        finally
        {
            await gameContext.DisposeAsync().ConfigureAwait(false);
            await otherContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The extra monsters spawn on each of their maps with their count per map. Killing the reapers on an
    /// extra map doesn't end the event; killing the ones of the main map does, and then the extra monsters disappear too.
    /// </summary>
    [Test]
    public async ValueTask ExtraMonstersSpawnPerMapAndOnlyMainReapersEndTheEventAsync()
    {
        var (gameContext, plugIn) = CreateContextAndPlugIn();
        try
        {
            AddMap(gameContext, ExtraMap, withEntrance: true);
            AddMap(gameContext, OtherExtraMap, withEntrance: true);
            plugIn.Configuration!.ExtraMobs =
            [
                new(1113, 5, [ExtraMap, OtherExtraMap], SpawnMapStrategy.AllMaps),
                new(1112, 3, [ExtraMap], SpawnMapStrategy.AllMaps),
            ];

            var mainMap = (await gameContext.GetMapAsync(TestMap).ConfigureAwait(false))!;
            var extraMap = (await gameContext.GetMapAsync(ExtraMap).ConfigureAwait(false))!;
            var otherExtraMap = (await gameContext.GetMapAsync(OtherExtraMap).ConfigureAwait(false))!;

            plugIn.StartByGameMaster(gameContext, TestMap);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false); // prepare
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false); // start + spawn

            Assert.That(CountLiving(extraMap, 1113), Is.EqualTo(5));
            Assert.That(CountLiving(otherExtraMap, 1113), Is.EqualTo(5), "The count is per map.");
            Assert.That(CountLiving(extraMap, 1112), Is.EqualTo(3));
            Assert.That(CountLivingEventMonsters(mainMap), Is.EqualTo(22));

            var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
            foreach (var reaper in GetLiving(extraMap, 1112))
            {
                await KillAsync(reaper, killer).ConfigureAwait(false);
            }

            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            Assert.That(plugIn.IsRunning(gameContext), Is.True, "The reapers of an extra map don't end the event.");

            foreach (var reaper in GetLiving(mainMap, 1112))
            {
                await KillAsync(reaper, killer).ConfigureAwait(false);
            }

            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            Assert.That(plugIn.IsRunning(gameContext), Is.False, "The reapers of the main map end the event.");
            Assert.That(CountLivingEventMonsters(extraMap), Is.Zero, "The extra monsters disappear at the end.");
            Assert.That(CountLivingEventMonsters(otherExtraMap), Is.Zero);
        }
        finally
        {
            await gameContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A game master start puts the main monsters on the requested map, and on a map of
    /// <see cref="DarkSkeletonInvasionConfiguration.TownDistanceMapIds"/> they keep the minimum distance to the town.
    /// </summary>
    [Test]
    public async ValueTask MainMonstersKeepDistanceToTownAsync()
    {
        var (gameContext, plugIn) = CreateContextAndPlugIn();
        try
        {
            AddMap(gameContext, TownMap, withTown: true);
            plugIn.Configuration!.TownDistanceMapIds = [TownMap];

            plugIn.StartByGameMaster(gameContext, TownMap);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);

            var townMap = (await gameContext.GetMapAsync(TownMap).ConfigureAwait(false))!;
            var testMap = (await gameContext.GetMapAsync(TestMap).ConfigureAwait(false))!;
            Assert.That(plugIn.GetMainMapId(gameContext), Is.EqualTo(TownMap));
            Assert.That(CountLivingEventMonsters(testMap), Is.Zero, "The daily map isn't used.");

            var monsters = GetLiving(townMap, 1111, 1112, 1113).ToList();
            Assert.That(monsters, Has.Count.EqualTo(22));
            foreach (var monster in monsters)
            {
                Assert.That(DistanceToArea(monster.Position, TownX1, TownY1, TownX2, TownY2), Is.GreaterThanOrEqualTo(20), $"{monster.Position} is too close to the town.");
            }
        }
        finally
        {
            await gameContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// On an extra map (like Kalima) the monsters spawn where players can walk to from the entrance, at least the
    /// minimum distance away from it - not on the cut-off map edge where the map's only safezone tiles are.
    /// </summary>
    [Test]
    public async ValueTask ExtraMonstersSpawnReachableFromEntranceAsync()
    {
        var (gameContext, plugIn) = CreateContextAndPlugIn();
        try
        {
            AddMap(gameContext, ExtraMap, withEntrance: true);
            plugIn.Configuration!.ExtraMobs = [new(1113, 100, [ExtraMap], SpawnMapStrategy.AllMaps)];

            plugIn.StartByGameMaster(gameContext, TestMap);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);

            var extraMap = (await gameContext.GetMapAsync(ExtraMap).ConfigureAwait(false))!;
            var monsters = GetLiving(extraMap, 1113).ToList();
            Assert.That(monsters, Has.Count.EqualTo(100));
            foreach (var monster in monsters)
            {
                Assert.That(monster.Position.Y, Is.LessThan(WallY), $"{monster.Position} is behind the wall, which can't be reached from the entrance.");
                Assert.That(DistanceToArea(monster.Position, GateX1, GateY1, GateX2, GateY2), Is.GreaterThanOrEqualTo(10), $"{monster.Position} is too close to the entrance.");
            }
        }
        finally
        {
            await gameContext.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static int CountLivingEventMonsters(GameMap map)
        => map.GetAttackablesInRange(new Pathfinding.Point(128, 128), 255).OfType<Monster>()
            .Count(m => m.Definition.Number is 1111 or 1112 or 1113 && m.IsAlive);

    private static int CountLiving(GameMap map, short number) => GetLiving(map, number).Count();

    private static IEnumerable<Monster> GetLiving(GameMap map, params short[] numbers)
        => map.GetAttackablesInRange(new Pathfinding.Point(128, 128), 255).OfType<Monster>()
            .Where(m => numbers.Contains(m.Definition.Number) && m.IsAlive)
            .ToList();

    private static int DistanceToArea(Pathfinding.Point point, int x1, int y1, int x2, int y2)
    {
        var dx = Math.Max(Math.Max(x1 - point.X, point.X - x2), 0);
        var dy = Math.Max(Math.Max(y1 - point.Y, point.Y - y2), 0);
        return Math.Max(dx, dy);
    }

    /// <summary>
    /// Adds a map whose terrain is walkable everywhere, optionally with a town (safezone square) in the middle,
    /// or with an entrance gate like Kalima, a wall which cuts off the bottom of the map and safezone tiles only
    /// on that cut-off bottom edge.
    /// </summary>
    private static void AddMap(GameContext gameContext, ushort number, bool withTown = false, bool withEntrance = false)
    {
        var terrain = new byte[ushort.MaxValue + 3];
        void Set(int x, int y, byte value) => terrain[3 + x + (y * 256)] = value;

        if (withTown)
        {
            for (var x = TownX1; x <= TownX2; x++)
            {
                for (var y = TownY1; y <= TownY2; y++)
                {
                    Set(x, y, 1);
                }
            }
        }

        var definition = new MUnique.OpenMU.Persistence.BasicModel.GameMapDefinition
        {
            Number = (short)number,
            Name = $"Test map {number}",
            TerrainData = terrain,
        };

        if (withEntrance)
        {
            for (var x = 0; x < 256; x++)
            {
                Set(x, WallY, 4);
                Set(x, 254, 1);
            }

            definition.ExitGates.Add(new MUnique.OpenMU.Persistence.BasicModel.ExitGate
            {
                Map = definition,
                X1 = GateX1,
                Y1 = GateY1,
                X2 = GateX2,
                Y2 = GateY2,
            });
        }

        gameContext.Configuration.Maps.Add(definition);
    }

    private static async ValueTask KillAsync(Monster monster, Player killer)
    {
        var hit = new HitInfo(100_000, 0, DamageAttributes.Undefined);
        await monster.ApplyResolvedHitAsync(killer, hit, new AttackKey(CombatIds.NextActionId(), 0)).ConfigureAwait(false);
        Assert.That(monster.IsAlive, Is.False, "The test hit must kill the monster.");
    }

    private static IGameContext CreateServerContext(byte id)
    {
        var context = new Mock<IGameServerContext>();
        context.Setup(c => c.Id).Returns(id);
        return context.Object;
    }

    private static (GameContext GameContext, DarkSkeletonInvasionPlugIn PlugIn) CreateContextAndPlugIn()
    {
        // The automatically discovered instance is deactivated, because it shares the event
        // state with the instance under test and would advance it by its own timer.
        var gameContext = (GameContext)GameContextTestHelper.CreateGameContext(
        [
            new PlugInConfiguration
            {
                TypeId = typeof(DarkSkeletonInvasionPlugIn).GUID,
                IsActive = false,
            },
        ]);

        gameContext.Configuration.Monsters.Add(CreateMonster(1111, "Dark Skeleton"));
        gameContext.Configuration.Monsters.Add(CreateMonster(1112, "Dark Reaper"));
        gameContext.Configuration.Monsters.Add(CreateMonster(1113, "Dark Mage"));

        var config = DarkSkeletonInvasionConfiguration.Default;
        config.PreStartMessageDelay = TimeSpan.Zero;
        foreach (var mob in config.Mobs)
        {
            mob.MapIds = [TestMap];
        }

        config.ExtraMobs = [];
        config.TownDistanceMapIds = [];

        var plugIn = new DarkSkeletonInvasionPlugIn { Configuration = config };
        return (gameContext, plugIn);
    }

    private static MonsterDefinition CreateMonster(short number, string name)
    {
        var definition = new MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition
        {
            // An own id, because the monster attributes are cached per definition id (shared with other tests).
            Id = Guid.NewGuid(),
            Number = number,
            Designation = name,
            ObjectKind = NpcObjectKind.Monster,
        };
        AddAttribute(definition, GameLogic.Attributes.Stats.MaximumHealth, 1000);
        AddAttribute(definition, GameLogic.Attributes.Stats.MinimumPhysBaseDmg, 100);
        AddAttribute(definition, GameLogic.Attributes.Stats.MaximumPhysBaseDmg, 200);
        AddAttribute(definition, GameLogic.Attributes.Stats.DefenseBase, 100);
        AddAttribute(definition, GameLogic.Attributes.Stats.AttackRatePvm, 400);
        AddAttribute(definition, GameLogic.Attributes.Stats.DefenseRatePvm, 300);
        return definition;
    }

    private static void AddAttribute(MonsterDefinition definition, AttributeSystem.AttributeDefinition attribute, float value)
    {
        definition.Attributes.Add(new MUnique.OpenMU.Persistence.BasicModel.MonsterAttribute
        {
            AttributeDefinition = attribute,
            Value = value,
        });
    }
}
