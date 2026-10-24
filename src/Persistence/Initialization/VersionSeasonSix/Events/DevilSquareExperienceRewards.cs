// <copyright file="DevilSquareExperienceRewards.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Configures the reviewed fixed Devil Square completion EXP amounts.
/// </summary>
public sealed class DevilSquareExperienceRewards : InitializerBase
{
    private const int Million = 1_000_000;

    private static readonly int[][] RewardMillions =
    [
        [8, 7, 6, 5],
        [13, 11, 10, 8],
        [19, 17, 15, 12],
        [28, 24, 21, 17],
        [38, 34, 29, 23],
        [54, 48, 42, 33],
        [78, 69, 60, 47],
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="DevilSquareExperienceRewards"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    public DevilSquareExperienceRewards(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        foreach (var game in this.GameConfiguration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.DevilSquare && game.GameLevel >= 1 && game.GameLevel <= RewardMillions.Length))
        {
            var ranks = RewardMillions[game.GameLevel - 1];
            foreach (var reward in game.Rewards.Where(reward => reward.RewardType == MiniGameRewardType.Experience && reward.Rank >= 1 && reward.Rank <= ranks.Length))
            {
                reward.RewardAmount = ranks[reward.Rank!.Value - 1] * Million;
            }
        }
    }
}
