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
        [10, 8, 6, 4],
        [15, 12, 10, 8],
        [20, 18, 15, 12],
        [30, 25, 22, 18],
        [40, 35, 30, 25],
        [55, 50, 45, 40],
        [60, 55, 50, 45],
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
        => this.ApplyToLevel(null);

    /// <summary>
    /// Applies the reviewed amounts only to the requested level.
    /// </summary>
    /// <param name="level">The DS level to update.</param>
    public void InitializeLevel(byte level)
        => this.ApplyToLevel(level);

    private void ApplyToLevel(byte? level)
    {
        foreach (var game in this.GameConfiguration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.DevilSquare && game.GameLevel >= 1 && game.GameLevel <= RewardMillions.Length && (level is null || game.GameLevel == level)))
        {
            var ranks = RewardMillions[game.GameLevel - 1];
            foreach (var reward in game.Rewards.Where(reward => reward.RewardType == MiniGameRewardType.Experience && reward.Rank >= 1 && reward.Rank <= ranks.Length))
            {
                reward.RewardAmount = ranks[reward.Rank!.Value - 1] * Million;
            }
        }
    }
}
