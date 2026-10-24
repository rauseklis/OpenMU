// <copyright file="BloodCastleCompletionRewards.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Configures Blood Castle completion payouts without changing experience, scores or level eight.
/// </summary>
public sealed class BloodCastleCompletionRewards : InitializerBase
{
    private const int ZenPerLevel = 1_000_000;
    private const byte LastJewelLevel = 4;
    private const byte LastChangedLevel = 7;
    private const MiniGameSuccessFlags CompletionFlags = MiniGameSuccessFlags.WinnerOrInWinningParty | MiniGameSuccessFlags.Alive;

    /// <summary>
    /// Initializes a new instance of the <see cref="BloodCastleCompletionRewards"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    public BloodCastleCompletionRewards(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        foreach (var game in this.GameConfiguration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.BloodCastle && game.GameLevel is >= 1 and <= LastChangedLevel))
        {
            this.ConfigurePayout(game);
        }
    }

    private void ConfigurePayout(MiniGameDefinition game)
    {
        foreach (var previous in game.Rewards.Where(reward => reward.RewardType is MiniGameRewardType.Money or MiniGameRewardType.Item or MiniGameRewardType.ItemDrop).ToArray())
        {
            game.Rewards.Remove(previous);
        }

        var group = this.Context.CreateNew<DropItemGroup>();
        group.Description = $"Blood Castle {game.GameLevel} completion items";
        group.Chance = 1;
        group.ItemType = SpecialItemType.RandomItem;
        group.ItemLevel = 0;
        this.GameConfiguration.DropItemGroups.Add(group);
        if (game.GameLevel <= LastJewelLevel)
        {
            this.ConfigureJewelsAndZen(game, group);
        }
        else
        {
            var number = game.GameLevel switch { 5 => 33, 6 => 34, _ => 32 };
            group.PossibleItems.Add(this.GameConfiguration.Items.Single(item => item.Group == 12 && item.Number == number));
        }

        var reward = this.Context.CreateNew<MiniGameReward>();
        reward.RewardType = MiniGameRewardType.Item;
        reward.RewardAmount = game.GameLevel <= LastJewelLevel ? game.GameLevel : 1;
        reward.RequiredSuccess = CompletionFlags;
        reward.ItemReward = group;
        game.Rewards.Add(reward);
    }

    private void ConfigureJewelsAndZen(MiniGameDefinition game, DropItemGroup group)
    {
        foreach (var jewel in new (byte Group, short Number)[] { (12, 15), (14, 13), (14, 14), (14, 16) })
        {
            group.PossibleItems.Add(this.GameConfiguration.Items.Single(item => item.Group == jewel.Group && item.Number == jewel.Number));
        }

        var money = this.Context.CreateNew<MiniGameReward>();
        money.RewardType = MiniGameRewardType.Money;
        money.RewardAmount = game.GameLevel * ZenPerLevel;
        money.RequiredSuccess = CompletionFlags;
        game.Rewards.Add(money);
    }
}
