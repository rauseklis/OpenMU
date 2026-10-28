// <copyright file="ChaosCastleBoxRewardTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Pins the independent Chaos Castle winner rewards on both shared worlds.
/// </summary>
[TestFixture]
public class ChaosCastleBoxRewardTests
{
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _configuration = null!;

    /// <summary>Creates disposable in-memory worlds, without connecting to a database.</summary>
    [OneTimeSetUp]
    public async Task InitializeAsync()
    {
        this._provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(this._provider, NullLoggerFactory.Instance).CreateInitialDataAsync(2, false).ConfigureAwait(false);
        using var context = this._provider.CreateNewConfigurationContext();
        this._configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
    }

    /// <summary>The jewel roll remains unchanged, and the second reward is a box, not equipment.</summary>
    [TestCase(1, 0.0, new int[0])]
    [TestCase(2, 0.05, new[] { 11 })]
    [TestCase(3, 0.10, new[] { 11 })]
    [TestCase(4, 0.15, new[] { 11 })]
    [TestCase(5, 0.20, new[] { 11, 124 })]
    [TestCase(6, 0.25, new[] { 124 })]
    [TestCase(7, 0.30, new[] { 124, 123 })]
    public void ApprovedRewards(byte level, double ancientChance, int[] boxNumbers)
    {
        var game = this._configuration.MiniGameDefinitions.Single(game => game.Type == MiniGameType.ChaosCastle && game.GameLevel == level);
        var jewel = game.Rewards.Single(reward => reward.ItemReward!.PossibleItems.Any(item => item.Group == 12 && item.Number == 15));
        Assert.That(jewel.ItemReward!.Chance, Is.EqualTo(0.9));
        Assert.That(jewel.ItemReward.PossibleItems.Select(item => ((int)item.Group, (int)item.Number)),
            Is.EquivalentTo(new[] { (12, 15), (14, 13), (14, 14), (14, 16), (14, 22) }));
        Assert.That(game.Rewards.Count, Is.EqualTo(level == 1 ? 1 : 2), "CC1 has no Ancient entry: a zero chance means legacy guaranteed delivery.");
        foreach (var reward in game.Rewards)
        {
            Assert.That(reward.RequiredSuccess, Is.EqualTo(MiniGameSuccessFlags.Winner | MiniGameSuccessFlags.Alive));
            Assert.That(reward.RewardType, Is.EqualTo(MiniGameRewardType.ItemDrop));
            Assert.That(reward.RewardAmount, Is.EqualTo(1));
        }

        if (level == 1)
        {
            return;
        }

        var boxes = game.Rewards.Single(reward => reward != jewel).ItemReward!;
        Assert.That(boxes.Chance, Is.EqualTo(ancientChance).Within(1e-12));
        Assert.That(boxes.ItemType, Is.EqualTo(SpecialItemType.RandomItem));
        Assert.That(boxes.ItemLevel, Is.Zero);
        Assert.That(boxes.PossibleItems.Select(item => (int)item.Number), Is.EquivalentTo(boxNumbers));
        Assert.That(boxes.PossibleItems.All(item => item.Group == 14), Is.True);
    }

    /// <summary>Both server definitions use the same approved reward configuration.</summary>
    [Test]
    public async Task BothWorldsShareRewardsAsync()
    {
        using var context = this._provider.CreateNewContext();
        var servers = (await context.GetAsync<GameServerDefinition>().ConfigureAwait(false)).ToArray();
        Assert.That(servers, Has.Length.EqualTo(2));
        Assert.That(servers.All(server => server.GameConfiguration == this._configuration), Is.True);
    }

    /// <summary>Exercises every real item-selection slot through the CC generator.</summary>
    [TestCase(5, 11, 124, 4, 1)]
    [TestCase(7, 124, 123, 17, 3)]
    public void MixedBoxesHaveExactConditionalWeights(byte level, int firstBox, int secondBox, int firstCount, int secondCount)
    {
        var game = this._configuration.MiniGameDefinitions.Single(game => game.Type == MiniGameType.ChaosCastle && game.GameLevel == level);
        var boxes = game.Rewards.Single(reward => reward.ItemReward!.PossibleItems.All(item => item.Group == 14 && item.Number is 11 or 123 or 124)).ItemReward!;
        var selectedSlot = 0;
        var slots = firstCount + secondCount;
        var random = new Mock<IRandomizer>();
        random.Setup(random => random.NextInt(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((int minimum, int maximum) => minimum == 0 && maximum == slots ? selectedSlot : minimum);
        var context = new Mock<IGameContext>();
        context.SetupGet(context => context.Configuration).Returns(this._configuration);
        context.SetupGet(context => context.DropGenerator).Returns(new DefaultDropGenerator(this._configuration, random.Object));
        var generator = new ChaosCastleDropGenerator(context.Object, game, 100);
        var results = new List<int>();
        for (selectedSlot = 0; selectedSlot < slots; selectedSlot++)
        {
            var item = generator.GenerateItemDrop(boxes)!;
            results.Add(item.Definition!.Number);
            Assert.That(item.Level, Is.Zero);
            Assert.That(item.ItemSetGroups, Is.Empty, "The prize must be a box, not Ancient equipment.");
        }

        Assert.That(results.Count(number => number == firstBox), Is.EqualTo(firstCount));
        Assert.That(results.Count(number => number == secondBox), Is.EqualTo(secondCount));
        Assert.That(boxes.PossibleItems.Count, Is.EqualTo(2), "Never persist repeated weighting slots.");
        Assert.That(boxes.Chance * firstCount / slots, Is.EqualTo(level == 5 ? .16 : .255).Within(1e-12));
        Assert.That(boxes.Chance * secondCount / slots, Is.EqualTo(level == 5 ? .04 : .045).Within(1e-12));
    }

    /// <summary>Successful independent rolls deliver exactly one jewel and one box only to an eligible winner.</summary>
    [TestCase(true, true, true, 2)]
    [TestCase(false, true, true, 0)]
    [TestCase(true, false, true, 0)]
    [TestCase(true, true, false, 0)]
    public async Task DeliveryPreservesEligibilityAsync(bool winner, bool alive, bool onMap, int expectedDrops)
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var map = new GameMap(gameContext.Configuration.Maps.Single(), TimeSpan.FromMinutes(1), 8);
        player.CurrentMap = onMap ? map : null;
        player.Attributes![Stats.CurrentHealth] = alive ? 35 : 0;
        player.IsAlive = alive;
        Assert.That(player.IsAlive, Is.EqualTo(alive));
        using var context = this._provider.CreateNewContext();
        var definition = context.CreateNew<MiniGameDefinition>();
        definition.GameLevel = 5;
        definition.Type = MiniGameType.ChaosCastle;
        var source = this._configuration.MiniGameDefinitions.Single(game => game.Type == MiniGameType.ChaosCastle && game.GameLevel == 5);
        foreach (var original in source.Rewards)
        {
            var reward = context.CreateNew<MiniGameReward>();
            reward.RequiredSuccess = original.RequiredSuccess;
            reward.RewardAmount = original.RewardAmount;
            reward.RewardType = original.RewardType;
            var group = context.CreateNew<DropItemGroup>();
            group.Chance = 1; // Force both independent successes, without depending on a random test result.
            group.ItemType = original.ItemReward!.ItemType;
            group.ItemLevel = original.ItemReward.ItemLevel;
            foreach (var item in original.ItemReward.PossibleItems)
            {
                group.PossibleItems.Add(item);
            }

            reward.ItemReward = group;
            definition.Rewards.Add(reward);
        }

        var random = new Mock<IRandomizer>();
        random.Setup(random => random.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns((int min, int max) => min);
        var rewardContext = new Mock<IGameContext>();
        rewardContext.SetupGet(context => context.Configuration).Returns(this._configuration);
        rewardContext.SetupGet(context => context.DropGenerator).Returns(new DefaultDropGenerator(this._configuration, random.Object));
        var generator = new ChaosCastleDropGenerator(rewardContext.Object, definition, 100);
        var service = new MiniGameRewardService(definition, gameContext, map, () => generator, () => TimeSpan.Zero,
            () => winner ? player : null, NullLogger<MiniGameRewardService>.Instance, definition);
        await service.GiveRewardsAndGetBonusScoreAsync(player, 1).ConfigureAwait(false);
        var drops = map.GetDropsInRange(player.Position, 16).OfType<DroppedItem>().ToArray();
        Assert.That(drops, Has.Length.EqualTo(expectedDrops));
    }

    /// <summary>The upgrade leaves jewels, other events and box contents intact, and is not reinstalled.</summary>
    [Test]
    public async Task UpgradeIsTargetedAndOneTimeAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(2, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var unchanged = config.MiniGameDefinitions.Where(game => game.Type != MiniGameType.ChaosCastle)
            .SelectMany(game => game.Rewards).Select(reward => (reward, reward.RewardAmount, reward.RequiredSuccess, reward.ItemReward)).ToArray();
        var jewels = config.MiniGameDefinitions.Where(game => game.Type == MiniGameType.ChaosCastle)
            .SelectMany(game => game.Rewards).Where(reward => reward.ItemReward!.PossibleItems.Any(item => item.Group == 12 && item.Number == 15))
            .Select(reward => (reward, reward.ItemReward, reward.ItemReward!.Chance)).ToArray();
        var boxContents = config.Items.SelectMany(item => item.DropItems)
            .Select(group => (group, group.Chance, Items: group.PossibleItems.ToArray())).ToArray();
        foreach (var game in config.MiniGameDefinitions.Where(game => game.Type == MiniGameType.ChaosCastle && game.GameLevel > 1))
        {
            var ancient = game.Rewards.Single(reward => !jewels.Any(jewel => jewel.reward == reward)).ItemReward!;
            ancient.ItemType = SpecialItemType.Ancient;
            ancient.Chance = .1 * game.GameLevel;
            ancient.PossibleItems.Clear();
        }

        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(update => update.Version == (int)UpdateVersion.ChaosCastleBoxRewards).ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await ChaosCastleBoxRewardsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        new ChaosCastleBoxRewards(context, config).Initialize();
        Assert.That(config.MiniGameDefinitions.Where(game => game.Type == MiniGameType.ChaosCastle).Sum(game => game.Rewards.Count), Is.EqualTo(13));
        foreach (var (reward, amount, flags, group) in unchanged)
        {
            Assert.That(reward.RewardAmount, Is.EqualTo(amount));
            Assert.That(reward.RequiredSuccess, Is.EqualTo(flags));
            Assert.That(reward.ItemReward, Is.SameAs(group));
        }

        foreach (var (reward, group, chance) in jewels)
        {
            Assert.That(reward.ItemReward, Is.SameAs(group));
            Assert.That(group!.Chance, Is.EqualTo(chance));
        }

        foreach (var (group, chance, items) in boxContents)
        {
            Assert.That(group.Chance, Is.EqualTo(chance));
            Assert.That(group.PossibleItems, Is.EqualTo(items));
        }

        var edited = config.MiniGameDefinitions.Single(game => game.Type == MiniGameType.ChaosCastle && game.GameLevel == 7)
            .Rewards.Single(reward => reward.ItemReward!.PossibleItems.Any(item => item.Number == 123)).ItemReward!;
        edited.Chance = .123;
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await ChaosCastleBoxRewardsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        Assert.That(edited.Chance, Is.EqualTo(.123));
    }
}
