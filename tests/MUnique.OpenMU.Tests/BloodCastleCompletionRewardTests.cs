// <copyright file="BloodCastleCompletionRewardTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Pins the payouts for successfully completing Blood Castle levels one through seven.
/// </summary>
[TestFixture]
public class BloodCastleCompletionRewardTests
{
    [Test]
    public async Task RepairIsOneTimePreservesCustomPayoutsAndProtectsEventGroupsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var castles = configuration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.BloodCastle).ToDictionary(game => game.GameLevel);
        var preserved = castles.Values.SelectMany(game => game.Rewards)
            .Where(reward => reward.RewardType != MiniGameRewardType.Experience)
            .Select(reward => (reward, reward.RewardAmount, reward.RequiredSuccess)).ToArray();
        var payouts = castles.Where(pair => pair.Key is >= 5 and <= 7)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Rewards.Single(reward => reward.RewardType == MiniGameRewardType.Item).ItemReward!);
        payouts[5].PossibleItems.Clear();
        payouts[7].PossibleItems.Clear();
        var custom = configuration.Items.Single(item => item.Group == 14 && item.Number == 13);
        payouts[6].PossibleItems.Clear();
        payouts[6].PossibleItems.Add(custom);
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
            .Where(update => update.Version == (int)UpdateVersion.BloodCastleRewardBalance).ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await BloodCastleRewardBalanceInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(payouts[5].PossibleItems.Single().Number, Is.EqualTo(33));
        Assert.That(payouts[7].PossibleItems.Single().Number, Is.EqualTo(32));
        Assert.That(payouts[6].PossibleItems.Single(), Is.SameAs(custom), "Never replace a nonempty custom payout.");
        foreach (var (reward, amount, flags) in preserved)
        {
            Assert.That(reward.RewardAmount, Is.EqualTo(amount));
            Assert.That(reward.RequiredSuccess, Is.EqualTo(flags));
            Assert.That(castles.Values.SelectMany(game => game.Rewards), Does.Contain(reward));
        }

        var eventGroups = configuration.MiniGameDefinitions.SelectMany(game => game.Rewards)
            .Where(reward => reward.ItemReward is not null).Select(reward => reward.ItemReward!).Distinct()
            .ToDictionary(group => group, group => group.PossibleItems.ToArray());
        new BonusProgression(context, configuration).Initialize();
        foreach (var (group, items) in eventGroups)
        {
            Assert.That(group.PossibleItems, Is.EquivalentTo(items));
        }

        var edited = castles[7].Rewards.First(reward => reward.RewardType == MiniGameRewardType.Experience);
        edited.RewardAmount = 123456;
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await BloodCastleRewardBalanceInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        Assert.That(edited.RewardAmount, Is.EqualTo(123456), "Later administrator edits must survive restarts.");
    }

    /// <summary>
    /// Lower levels give Zen and jewels, upper levels always give the appropriate box.
    /// </summary>
    [Test]
    public async Task SuccessfulCompletionUsesApprovedPayoutsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var unchanged = configuration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.BloodCastle)
            .SelectMany(game => game.Rewards.Where(reward => game.GameLevel == 8 || reward.RewardType is MiniGameRewardType.Experience or MiniGameRewardType.ExperiencePerRemainingSeconds or MiniGameRewardType.Score))
            .ToArray();
        new BloodCastleCompletionRewards(context, configuration).Initialize();
        var after = configuration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.BloodCastle)
            .SelectMany(game => game.Rewards).ToArray();
        Assert.That(unchanged.All(after.Contains), Is.True, "BC +8, experience and scoring must remain unchanged.");
        foreach (var game in configuration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.BloodCastle && game.GameLevel <= 7))
        {
            var payouts = game.Rewards.Where(reward => reward.RewardType is MiniGameRewardType.Item or MiniGameRewardType.ItemDrop or MiniGameRewardType.Money).ToArray();
            var items = payouts.Single(reward => reward.RewardType == MiniGameRewardType.Item);
            Assert.That(items.ItemReward!.Chance, Is.EqualTo(1));
            if (game.GameLevel <= 4)
            {
                Assert.That(payouts.Single(reward => reward.RewardType == MiniGameRewardType.Money).RewardAmount, Is.EqualTo(game.GameLevel * 1_000_000));
                Assert.That(items.RewardAmount, Is.EqualTo(game.GameLevel));
                Assert.That(items.ItemReward.PossibleItems.Select(item => ((int)item.Group, (int)item.Number)), Is.EquivalentTo(new[] { (12, 15), (14, 13), (14, 14), (14, 16) }));
            }
            else
            {
                Assert.That(payouts, Has.Length.EqualTo(1));
                Assert.That(items.RewardAmount, Is.EqualTo(1));
                var box = items.ItemReward.PossibleItems.Single();
                Assert.That(box.Group, Is.EqualTo(12));
                Assert.That(box.Number, Is.EqualTo(game.GameLevel switch { 5 => 33, 6 => 34, _ => 32 }));
            }
        }
    }
}
