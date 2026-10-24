// <copyright file="DevilSquareZenRewardTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Verifies all Devil Square Zen payouts and preservation of experience and BC rewards.
/// </summary>
[TestFixture]
public class DevilSquareZenRewardTests
{
    /// <summary>
    /// Each rank gets the reviewed Zen and EXP amounts without repeated multiplication.
    /// </summary>
    [Test]
    public async Task ReviewedZenAndExperienceAmountsAreStableAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        int[][] zen = [[1_500_000, 1_250_000, 1_000_000, 750_000], [2_000_000, 1_750_000, 1_500_000, 1_250_000], [2_500_000, 2_250_000, 2_000_000, 1_750_000], [3_000_000, 2_750_000, 2_500_000, 2_250_000], [3_500_000, 3_250_000, 3_000_000, 2_750_000], [4_000_000, 3_750_000, 3_500_000, 3_250_000], [7_500_000, 7_000_000, 6_000_000, 4_500_000]];
        int[][] experience = [[8_000_000, 7_000_000, 6_000_000, 5_000_000], [13_000_000, 11_000_000, 10_000_000, 8_000_000], [19_000_000, 17_000_000, 15_000_000, 12_000_000], [28_000_000, 24_000_000, 21_000_000, 17_000_000], [38_000_000, 34_000_000, 29_000_000, 23_000_000], [54_000_000, 48_000_000, 42_000_000, 33_000_000], [78_000_000, 69_000_000, 60_000_000, 47_000_000]];
        var bloodCastle = configuration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.BloodCastle)
            .SelectMany(game => game.Rewards).Select(reward => (reward, reward.RewardAmount)).ToArray();
        new DevilSquareZenRewards(context, configuration).Initialize();
        new DevilSquareZenRewards(context, configuration).Initialize();
        new DevilSquareExperienceRewards(context, configuration).Initialize();
        new DevilSquareExperienceRewards(context, configuration).Initialize();
        foreach (var game in configuration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.DevilSquare))
        {
            foreach (var reward in game.Rewards.Where(reward => reward.RewardType is MiniGameRewardType.Money or MiniGameRewardType.Experience))
            {
                var expected = reward.RewardType == MiniGameRewardType.Money ? zen : experience;
                Assert.That(reward.RewardAmount, Is.EqualTo(expected[game.GameLevel - 1][reward.Rank!.Value - 1]));
            }
        }

        Assert.That(bloodCastle.All(previous => previous.reward.RewardAmount == previous.RewardAmount), Is.True);
    }

    /// <summary>
    /// DS one through six grant only first place the matching BC items, without a DS seven red box.
    /// </summary>
    [Test]
    public async Task InventoryItemsAreFirstPlaceOnlyAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        new DevilSquareFirstPlaceItems(context, configuration).Initialize();
        foreach (var square in configuration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.DevilSquare))
        {
            var itemRewards = square.Rewards.Where(reward => reward.RewardType is MiniGameRewardType.Item or MiniGameRewardType.ItemDrop).ToArray();
            if (square.GameLevel == 7)
            {
                Assert.That(itemRewards, Is.Empty);
                continue;
            }

            Assert.That(itemRewards, Has.Length.EqualTo(1));
            var reward = itemRewards.Single();
            Assert.That(reward.Rank, Is.EqualTo(1));
            Assert.That(reward.RewardType, Is.EqualTo(MiniGameRewardType.Item));
            var castle = configuration.MiniGameDefinitions.Single(game => game.Type == MiniGameType.BloodCastle && game.GameLevel == square.GameLevel);
            var expected = castle.Rewards.Single(reward => reward.RewardType == MiniGameRewardType.Item);
            Assert.That(reward.ItemReward, Is.SameAs(expected.ItemReward));
            Assert.That(reward.RewardAmount, Is.EqualTo(expected.RewardAmount));
        }
    }
}
