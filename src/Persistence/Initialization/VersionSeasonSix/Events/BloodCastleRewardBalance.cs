// <copyright file="BloodCastleRewardBalance.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Applies fixed Blood Castle experience and repairs empty standard Ribbon payout pools.
/// </summary>
public sealed class BloodCastleRewardBalance : InitializerBase
{
    private static readonly (int Objective, int Completion)[] Experience =
    {
        (0, 0), (45_000, 70_000), (140_000, 210_000), (260_000, 370_000),
        (390_000, 510_000), (500_000, 590_000), (2_210_000, 2_230_000), (5_300_000, 3_500_000),
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="BloodCastleRewardBalance"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The selected game configuration.</param>
    public BloodCastleRewardBalance(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        foreach (var game in this.GameConfiguration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.BloodCastle && game.GameLevel is >= 1 and <= 7))
        {
            var amounts = Experience[game.GameLevel];
            foreach (var reward in game.Rewards.Where(reward => reward.RewardType == MiniGameRewardType.Experience))
            {
                if (reward.RequiredKill?.Number is 131 or 132)
                {
                    reward.RewardAmount = amounts.Objective;
                }
                else if (reward.RequiredKill is null && reward.RequiredSuccess == MiniGameSuccessFlags.WinnerOrInWinningParty)
                {
                    reward.RewardAmount = amounts.Completion;
                }
            }

            if (game.GameLevel < 5)
            {
                continue;
            }

            foreach (var reward in game.Rewards.Where(reward => reward.RewardType == MiniGameRewardType.Item
                && reward.RewardAmount == 1
                && reward.RequiredSuccess == (MiniGameSuccessFlags.WinnerOrInWinningParty | MiniGameSuccessFlags.Alive)))
            {
                if (reward.ItemReward is not { } group
                    || group.Description.Value != $"Blood Castle {game.GameLevel} completion items"
                    || group.Chance != 1 || group.ItemType != SpecialItemType.RandomItem || group.ItemLevel != 0
                    || group.PossibleItems.Count != 0)
                {
                    continue;
                }

                var number = game.GameLevel switch { 5 => 33, 6 => 34, _ => 32 };
                group.PossibleItems.Add(this.GameConfiguration.Items.Single(item => item.Group == 12 && item.Number == number));
            }
        }
    }
}
