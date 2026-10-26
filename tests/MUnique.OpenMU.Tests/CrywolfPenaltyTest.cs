// <copyright file="CrywolfPenaltyTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Crywolf;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;
using ItemDefinition = MUnique.OpenMU.Persistence.BasicModel.ItemDefinition;
using MonsterAttribute = MUnique.OpenMU.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests the penalties of an occupied crywolf fortress for the monsters of the hunting maps: less experience,
/// less jewels and more maximum health. They must not affect other maps, summons or NPCs.
/// </summary>
[TestFixture]
public class CrywolfPenaltyTest
{
    private const short NormalMonsterLevel = 100;

    /// <summary>
    /// Tests that the experience of an affected monster is reduced, once, and the experience of a monster
    /// on a map which isn't affected is not.
    /// </summary>
    [Test]
    public async Task ExperienceIsReducedForAffectedMonstersOnlyAsync()
    {
        var (plugIn, gameContext) = await CreatePlugInAsync(isOccupied: true, d =>
        {
            d.ExperiencePenaltyPercentage = 50;
            d.PenaltyMapNumbers = new List<short> { 0 };
        }).ConfigureAwait(false);
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IExperienceCalculationPlugIn>(plugIn);
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        PrepareForExperience(gameContext, player);
        var monster = await CreateMonsterAsync(gameContext).ConfigureAwait(false);
        var rateBefore = player.Attributes![Stats.ExperienceRate];

        var penalized = await player.CalculateExpAfterKillAsync(monster).ConfigureAwait(false);
        var definition = plugIn.Configuration!;
        definition.PenaltyMapNumbers = new List<short> { 10 };
        var normal = await player.CalculateExpAfterKillAsync(monster).ConfigureAwait(false);

        Assert.That(normal, Is.GreaterThan(1), "precondition");
        Assert.That(penalized, Is.EqualTo(normal / 2).Within(1), "The experience is halved exactly once.");
        Assert.That(player.Attributes[Stats.ExperienceRate], Is.EqualTo(rateBefore), "The rate of the player isn't changed.");
    }

    /// <summary>
    /// Tests that monsters below the minimum level are not affected, and that the master experience is reduced, too.
    /// </summary>
    [Test]
    public async Task MinimumLevelAndMasterExperienceAsync()
    {
        var (plugIn, gameContext) = await CreatePlugInAsync(isOccupied: true, d =>
        {
            d.ExperiencePenaltyPercentage = 50;
            d.PenaltyMapNumbers = new List<short> { 0 };
            d.PenaltyMinimumMonsterLevel = 150;
        }).ConfigureAwait(false);
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var weakMonster = await CreateMonsterAsync(gameContext, level: 100).ConfigureAwait(false);
        var strongMonster = await CreateMonsterAsync(gameContext, level: 200).ConfigureAwait(false);

        var weak = new ExperienceCalculationArgs(weakMonster, false, 1000);
        var strong = new ExperienceCalculationArgs(strongMonster, false, 1000);
        var strongMaster = new ExperienceCalculationArgs(strongMonster, true, 1000);
        await plugIn.CalculateExperienceAsync(player, weak).ConfigureAwait(false);
        await plugIn.CalculateExperienceAsync(player, strong).ConfigureAwait(false);
        await plugIn.CalculateExperienceAsync(player, strongMaster).ConfigureAwait(false);

        Assert.That(weak.Experience, Is.EqualTo(1000), "Below the minimum level.");
        Assert.That(strong.Experience, Is.EqualTo(500));
        Assert.That(strongMaster.Experience, Is.EqualTo(500), "Master experience, too.");
    }

    /// <summary>
    /// Tests that nothing is reduced while the fortress is in peace.
    /// </summary>
    [Test]
    public async Task NothingIsReducedInPeaceAsync()
    {
        var (plugIn, gameContext) = await CreatePlugInAsync(isOccupied: false, d =>
        {
            d.ExperiencePenaltyPercentage = 50;
            d.PenaltyMapNumbers = new List<short> { 0 };
        }).ConfigureAwait(false);
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var monster = await CreateMonsterAsync(gameContext).ConfigureAwait(false);

        var args = new ExperienceCalculationArgs(monster, false, 1000);
        await plugIn.CalculateExperienceAsync(player, args).ConfigureAwait(false);

        Assert.That(args.Experience, Is.EqualTo(1000));
    }

    /// <summary>
    /// Tests that the experience of the members of a party is reduced exactly once, too.
    /// </summary>
    [Test]
    public async Task PartyExperienceIsReducedOnceAsync()
    {
        var (plugIn, gameContext) = await CreatePlugInAsync(isOccupied: true, d =>
        {
            d.ExperiencePenaltyPercentage = 50;
            d.PenaltyMapNumbers = new List<short> { 0 };
        }).ConfigureAwait(false);
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IExperienceCalculationPlugIn>(plugIn);
        var first = await CreatePlayerInGameAsync(gameContext).ConfigureAwait(false);
        var second = await CreatePlayerInGameAsync(gameContext).ConfigureAwait(false);
        PrepareForExperience(gameContext, first, second);
        var party = new Party(new PartyManager(5, new NullLogger<Party>()), 5, new NullLogger<Party>());
        await party.AddAsync(first).ConfigureAwait(false);
        await party.AddAsync(second).ConfigureAwait(false);
        await first.AddObserverAsync(second).ConfigureAwait(false);
        var monster = await CreateMonsterAsync(gameContext).ConfigureAwait(false);

        var penalized = await party.DistributeExperienceAfterKillAsync(monster, first).ConfigureAwait(false);
        plugIn.Configuration!.PenaltyMapNumbers = new List<short> { 10 };
        var normal = await party.DistributeExperienceAfterKillAsync(monster, first).ConfigureAwait(false);

        var penalizedTotal = penalized.Sum(share => share.Experience);
        var normalTotal = normal.Sum(share => share.Experience);
        Assert.That(normalTotal, Is.GreaterThan(2), "precondition");
        Assert.That(penalizedTotal, Is.EqualTo(normalTotal / 2).Within(2));
    }

    /// <summary>
    /// Tests that the jewels of affected monsters only drop with the configured chance, and unaffected monsters drop normally.
    /// </summary>
    [Test]
    public async Task JewelDropsAreReducedForAffectedMonstersOnlyAsync()
    {
        var (plugIn, gameContext) = await CreatePlugInAsync(isOccupied: true, d =>
        {
            d.JewelDropPenaltyPercentage = 0;
            d.PenaltyMapNumbers = new List<short> { 0 };
            d.PenaltyJewels = new List<CrywolfPenaltyItem> { new() { Group = 14, Number = 13 } };
        }).ConfigureAwait(false);
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var monster = await CreateMonsterAsync(gameContext).ConfigureAwait(false);

        Assert.That(IsDropCancelled(plugIn, monster, player), Is.True);

        plugIn.Configuration!.PenaltyMapNumbers = new List<short> { 10 };
        Assert.That(IsDropCancelled(plugIn, monster, player), Is.False, "The map isn't affected.");
    }

    /// <summary>
    /// Tests that the maximum health of affected monsters is raised by the penalty, that the benefit isn't
    /// changed by it, that updating doesn't heal or stack, and that the penalty can be removed again.
    /// </summary>
    [Test]
    public async Task MaximumHealthIsRaisedForAffectedMonstersOnlyAsync()
    {
        var (plugIn, gameContext) = await CreatePlugInAsync(isOccupied: true, d =>
        {
            d.PenaltyMapNumbers = new List<short> { 0 };
            d.MonsterHealthPenaltyPercentage = 110;
        }, execute: false).ConfigureAwait(false);
        var map = (await gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        var affected = await CreateMonsterAsync(gameContext).ConfigureAwait(false);
        await map.AddAsync(affected).ConfigureAwait(false);
        var owner = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var summoned = await CreateMonsterAsync(gameContext, intelligence: new SummonedMonsterIntelligence(owner)).ConfigureAwait(false);
        await plugIn.ObjectAddedToMapAsync(map, affected).ConfigureAwait(false);
        await plugIn.ObjectAddedToMapAsync(map, summoned).ConfigureAwait(false);

        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        Assert.That(affected.Attributes[Stats.MaximumHealth], Is.EqualTo(1100));
        Assert.That(summoned.Attributes[Stats.MaximumHealth], Is.EqualTo(1000), "Summons are not affected.");

        affected.Health = 500;
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        Assert.That(affected.Attributes[Stats.MaximumHealth], Is.EqualTo(1100), "It doesn't stack.");
        Assert.That(affected.Health, Is.EqualTo(500), "Updating doesn't heal.");

        affected.Health = 1100;
        plugIn.Configuration!.IsPenaltyActive = false;
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        Assert.That(affected.Attributes[Stats.MaximumHealth], Is.EqualTo(1000));
        Assert.That(affected.Health, Is.EqualTo(1000), "The health doesn't stay above the new maximum.");
    }

    private static void PrepareForExperience(GameContext gameContext, params Player[] players)
    {
        var configuration = gameContext.Configuration;
        configuration.MinimumMonsterLevelForMasterExperience = 0;
        configuration.ExperienceRate = 1.0f;
        configuration.MasterExperienceRate = 1.0f;
        foreach (var mapDefinition in configuration.Maps)
        {
            mapDefinition.ExpMultiplier = 1.0f;
        }

        foreach (var player in players)
        {
            player.Attributes![Stats.Level] = 1;
            player.Attributes[Stats.MasterLevel] = 0;
            player.Attributes[Stats.PointsPerLevelUp] = 1;
            player.Attributes[Stats.MasterPointsPerLevelUp] = 1;
            player.Attributes.AddElement(new SimpleElement(1.0f, AggregateType.AddRaw), Stats.ExperienceRate);
            player.Attributes.AddElement(new SimpleElement(1.0f, AggregateType.AddRaw), Stats.MasterExperienceRate);
            player.Attributes.AddElement(new SimpleElement(1, AggregateType.AddRaw), Stats.TotalLevel);
            player.SelectedCharacter!.Experience = 0;
        }
    }

    /// <summary>
    /// Tests that deactivating the plug-in undoes the raised maximum health of the monsters.
    /// </summary>
    [Test]
    public async Task DeactivationUndoesTheHealthPenaltyAsync()
    {
        var (plugIn, gameContext) = await CreatePlugInAsync(isOccupied: true, d =>
        {
            d.PenaltyMapNumbers = new List<short> { 0 };
            d.MonsterHealthPenaltyPercentage = 110;
        }, execute: false).ConfigureAwait(false);
        var map = (await gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        var monster = await CreateMonsterAsync(gameContext).ConfigureAwait(false);
        await plugIn.ObjectAddedToMapAsync(map, monster).ConfigureAwait(false);
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        Assert.That(monster.Attributes[Stats.MaximumHealth], Is.EqualTo(1100), "precondition");

        gameContext.PlugInManager.RegisterPlugIn<IPeriodicTaskPlugIn, CrywolfPlugIn>();
        gameContext.PlugInManager.DeactivatePlugIn<CrywolfPlugIn>();

        Assert.That(monster.Attributes[Stats.MaximumHealth], Is.EqualTo(1000));
    }

    /// <summary>
    /// Tests that only the jewels of the list drop less often while the fortress is occupied, and that they drop normally in peace.
    /// </summary>
    [Test]
    public async Task OnlyJewelsDropLessOftenWhileOccupiedAsync()
    {
        var (plugIn, gameContext) = await CreatePlugInAsync(isOccupied: true, d =>
        {
            d.JewelDropPenaltyPercentage = 0;
            d.PenaltyMapNumbers = new List<short> { 0 };
            d.PenaltyJewels = new List<CrywolfPenaltyItem> { new() { Group = 14, Number = 13 }, new() { Group = 12, Number = 15 } };
        }).ConfigureAwait(false);
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var monster = await CreateMonsterAsync(gameContext).ConfigureAwait(false);

        Assert.That(IsDropCancelled(plugIn, monster, player, 14, 13), Is.True, "Jewel of Bless");
        Assert.That(IsDropCancelled(plugIn, monster, player, 12, 15), Is.True, "Jewel of Chaos");
        Assert.That(IsDropCancelled(plugIn, monster, player, 14, 15), Is.False, "Zen isn't a jewel");
        Assert.That(IsDropCancelled(plugIn, monster, player, 14, 31), Is.False, "Jewel of Guardian isn't in the configured list");

        var (peacePlugIn, peaceContext) = await CreatePlugInAsync(isOccupied: false, d =>
        {
            d.JewelDropPenaltyPercentage = 0;
            d.PenaltyMapNumbers = new List<short> { 0 };
            d.PenaltyJewels = new List<CrywolfPenaltyItem> { new() { Group = 14, Number = 13 } };
        }).ConfigureAwait(false);
        var peaceMonster = await CreateMonsterAsync(peaceContext).ConfigureAwait(false);
        var peacePlayer = await PlayerTestHelper.CreatePlayerAsync(peaceContext).ConfigureAwait(false);
        Assert.That(IsDropCancelled(peacePlugIn, peaceMonster, peacePlayer, 14, 13), Is.False, "Peace");
    }

    /// <summary>
    /// Tests that the default configuration is the penalty of the original game: only the Jewel of Guardian drops
    /// less often, only in the Land of Trials, and that the experience and the health of monsters aren't changed.
    /// </summary>
    [Test]
    public void DefaultsAreThePenaltyOfTheOriginalGame()
    {
        var definition = new CrywolfEventDefinition();

        Assert.That(definition.PenaltyMapNumbers, Is.EqualTo(new short[] { 31 }), "Land of Trials");
        Assert.That(definition.PenaltyJewels.Select(item => (item.Group, item.Number)), Is.EqualTo(new[] { ((byte)14, (short)31) }), "Jewel of Guardian");
        Assert.That(definition.ExperiencePenaltyPercentage, Is.EqualTo(100));
        Assert.That(definition.MonsterHealthPenaltyPercentage, Is.EqualTo(100));
        Assert.That(definition.IsPenaltyActive, Is.False);
        Assert.That(definition.BenefitExcludedMapNumbers, Is.EqualTo(new short[] { 31 }), "Land of Trials");
        Assert.That(definition.StartDays, Is.EqualTo(new[] { DayOfWeek.Saturday }));
    }

    /// <summary>
    /// Tests that the monsters of the maps, which are excluded from the benefit, keep their health, like in the original game.
    /// </summary>
    [Test]
    public async Task BenefitDoesNotReduceTheHealthOfExcludedMapsAsync()
    {
        var (plugIn, gameContext) = await CreatePlugInAsync(isOccupied: false, d => d.BenefitExcludedMapNumbers = new List<short> { 0 }, execute: false).ConfigureAwait(false);
        var map = (await gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        var monster = await CreateMonsterAsync(gameContext).ConfigureAwait(false);
        await map.AddAsync(monster).ConfigureAwait(false);
        await plugIn.ObjectAddedToMapAsync(map, monster).ConfigureAwait(false);

        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        Assert.That(monster.Attributes[Stats.MaximumHealth], Is.EqualTo(1000), "The map is excluded.");

        plugIn.Configuration!.BenefitExcludedMapNumbers = new List<short> { 31 };
        var other = await CreateMonsterAsync(gameContext).ConfigureAwait(false);
        await map.AddAsync(other).ConfigureAwait(false);
        await plugIn.ObjectAddedToMapAsync(map, other).ConfigureAwait(false);
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        Assert.That(other.Attributes[Stats.MaximumHealth], Is.EqualTo(900), "The benefit applies to other maps.");
    }

    private static bool IsDropCancelled(CrywolfPlugIn plugIn, Monster monster, Player killer, byte group = 14, short number = 13)
    {
        var item = new MUnique.OpenMU.Persistence.BasicModel.Item { Definition = new ItemDefinition { Group = group, Number = number } };
        var eventArgs = new CancelEventArgs();
        plugIn.ItemDropping(monster, killer, item, eventArgs);
        return eventArgs.Cancel;
    }

    private static async ValueTask<Player> CreatePlayerInGameAsync(GameContext gameContext)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        player.IsAlive = true;
        await player.CurrentMap!.AddAsync(player).ConfigureAwait(false);
        return player;
    }

    private static async ValueTask<Monster> CreateMonsterAsync(GameContext gameContext, short level = NormalMonsterLevel, INpcIntelligence? intelligence = null)
    {
        var map = (await gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        var definition = new MonsterDefinition { Id = Guid.NewGuid(), ObjectKind = NpcObjectKind.Monster };
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 1000 });
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = level });
        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = definition,
            GameMap = map.Definition,
            X1 = 100,
            X2 = 100,
            Y1 = 100,
            Y2 = 100,
            Quantity = 1,
        };
        var monster = new Monster(spawnArea, definition, map, NullDropGenerator.Instance, intelligence ?? new Mock<INpcIntelligence>().Object, gameContext.PlugInManager, gameContext.PathFinderPool);
        monster.Initialize();
        return monster;
    }

    private static async ValueTask<(CrywolfPlugIn PlugIn, GameContext GameContext)> CreatePlugInAsync(bool isOccupied, Action<CrywolfEventDefinition> configure, bool execute = true)
    {
        var gameContext = (GameContext)GameContextTestHelper.CreateGameContext();
        using (var persistenceContext = gameContext.PersistenceContextProvider.CreateNewTypedContext(typeof(CrywolfData), false, gameContext.Configuration))
        {
            var data = persistenceContext.CreateNew<CrywolfData>();
            data.IsOccupied = isOccupied;
            data.LastBattleEnd = DateTime.UtcNow;
            await persistenceContext.SaveChangesAsync().ConfigureAwait(false);
        }

        // The map of the event doesn't exist in the test configuration. The occupation state applies anyway.
        var definition = new CrywolfEventDefinition { MapNumber = 999, IsPenaltyActive = true };
        configure(definition);
        var plugIn = new CrywolfPlugIn { Configuration = definition };
        if (execute)
        {
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        }

        return (plugIn, gameContext);
    }
}
