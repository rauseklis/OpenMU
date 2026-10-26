// <copyright file="CrywolfArmyTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Crywolf;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests the army of the crywolf event on the real map, with its real terrain and spawns.
/// </summary>
[TestFixture]
public class CrywolfArmyTest
{
    /// <summary>
    /// Tests that the leader of every group, which has to march to a goal, starts marching and gets closer to its goal.
    /// The paths of the army are long, so the search of the path must be able to find them.
    /// </summary>
    [Test]
    public async Task EveryGroupMarchesTowardsItsGoalAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new Persistence.Initialization.VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        var gameConfiguration = (await contextProvider.CreateNewContext().GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        var mapInitializer = new MapInitializer(gameConfiguration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(
            gameConfiguration,
            contextProvider,
            mapInitializer,
            new NullLoggerFactory(),
            new PlugInManager(new List<PlugInConfiguration> { new() { TypeId = typeof(CrywolfPlugIn).GUID, IsActive = false } }, new NullLoggerFactory(), null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;

        var definition = new CrywolfEventDefinition { ContractCharacterClassNumbers = new List<byte> { 0 }, MinimumContractLevel = 0, ContractDelay = TimeSpan.Zero };
        var context = new CrywolfContext(gameContext, definition);
        await context.InitializeAsync().ConfigureAwait(false);
        var map = context.Map!;
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        player.IsAlive = true;
        player.CurrentMap = map;
        player.Position = context.Altars[0].Npc.Position;
        await map.AddAsync(player).ConfigureAwait(false);
        for (var i = 0; i < 10 && context.State != CrywolfState.Ready; i++)
        {
            context.SkipWaitingTime();
            await context.TickAsync().ConfigureAwait(false);
        }

        await context.ContractAltarAsync(player, context.Altars[0].Npc.Id).ConfigureAwait(false);
        await context.TickAsync().ConfigureAwait(false);
        var leaders = map.GetNpcsInRange(new Point(128, 128), byte.MaxValue).OfType<Monster>()
            .Where(monster => monster.Definition.Number == definition.DarkElfNumber)
            .ToDictionary(monster => monster, monster => monster.Position);
        for (var i = 0; i < 10 && context.State != CrywolfState.Start; i++)
        {
            context.SkipWaitingTime();
            await context.TickAsync().ConfigureAwait(false);
        }

        Assert.That(context.State, Is.EqualTo(CrywolfState.Start), "precondition");
        var marching = leaders.Where(entry => definition.MonsterGroups.Any(group => group.WaveNumber == entry.Key.SpawnArea.WaveNumber
                                                                                     && (group.AdvanceGoalX, group.AdvanceGoalY) != (entry.Value.X, entry.Value.Y))).ToList();
        Assert.That(marching, Has.Count.GreaterThan(5), "precondition");

        // The army marches in real time, and it has to get clearly closer to its goal, not only move.
        var goals = definition.MonsterGroups.ToDictionary(group => group.WaveNumber, group => new Point(group.AdvanceGoalX, group.AdvanceGoalY));
        var initialDistances = marching.ToDictionary(entry => entry.Key, entry => entry.Value.EuclideanDistanceTo(goals[entry.Key.SpawnArea.WaveNumber]));
        var notCloser = marching.Select(entry => entry.Key).ToList();
        for (var i = 0; i < 400 && notCloser.Count > 0; i++)
        {
            await Task.Delay(200).ConfigureAwait(false);
            notCloser = notCloser.Where(monster => monster.Position.EuclideanDistanceTo(goals[monster.SpawnArea.WaveNumber]) > initialDistances[monster] - Math.Min(5, initialDistances[monster])).ToList();
        }

        Assert.That(notCloser.Select(monster => $"wave {monster.SpawnArea.WaveNumber} at {monster.Position}, goal {goals[monster.SpawnArea.WaveNumber]}"), Is.Empty, "The leaders which didn't get closer to their goal.");
    }

    /// <summary>
    /// Tests that a long way is walked in steps which the path finder of the monsters can calculate, and that
    /// the steps get shorter when the farther ones can't be reached.
    /// </summary>
    [Test]
    public void LongWaysAreWalkedInShortSteps()
    {
        var position = new Point(125, 77);
        var steps = CrywolfMonsterIntelligence.GetSteps(position, new Point(120, 58)).ToList();

        Assert.That(steps, Is.Not.Empty);
        Assert.That(steps.Select(step => Math.Max(Math.Abs(step.X - position.X), Math.Abs(step.Y - position.Y))), Is.All.LessThanOrEqualTo(16), "The path finder only searches a segment of 16 x 16 tiles.");
        Assert.That(steps.Select(step => step.EuclideanDistanceTo(position)), Is.Ordered.Descending, "The farthest step comes first.");
        Assert.That(CrywolfMonsterIntelligence.GetSteps(position, new Point(127, 80)), Is.EqualTo(new[] { new Point(127, 80) }), "A near target is walked to directly.");
    }
}
