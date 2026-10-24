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
        int[][] experience = [[10_000_000, 8_000_000, 6_000_000, 4_000_000], [15_000_000, 12_000_000, 10_000_000, 8_000_000], [20_000_000, 18_000_000, 15_000_000, 12_000_000], [30_000_000, 25_000_000, 22_000_000, 18_000_000], [40_000_000, 35_000_000, 30_000_000, 25_000_000], [55_000_000, 50_000_000, 45_000_000, 40_000_000], [60_000_000, 55_000_000, 50_000_000, 45_000_000]];
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
    /// Fresh DS configurations contain only the reviewed Zen and EXP payouts.
    /// </summary>
    [Test]
    public async Task CompletionRewardsContainNoItemsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        foreach (var square in configuration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.DevilSquare))
        {
            Assert.That(square.Rewards, Has.Count.EqualTo(8));
            Assert.That(square.Rewards.All(reward => reward.RewardType is MiniGameRewardType.Money or MiniGameRewardType.Experience), Is.True);
        }
    }
}
