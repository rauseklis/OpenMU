// <copyright file="DevilSquareZenRewards.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Applies fifty times the base Devil Square Zen rewards.
/// </summary>
public sealed class DevilSquareZenRewards : InitializerBase
{
    private const int ZenMultiplier = 50;
    private const byte LastLevel = 7;
    private const int LastRewardedRank = 4;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevilSquareZenRewards"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    public DevilSquareZenRewards(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        foreach (var game in this.GameConfiguration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.DevilSquare && game.GameLevel is >= 1 and <= LastLevel))
        {
            foreach (var reward in game.Rewards.Where(reward => reward.RewardType == MiniGameRewardType.Money && reward.Rank is >= 1 and <= LastRewardedRank))
            {
                reward.RewardAmount = Version095d.Events.DevilSquareInitializer.GetBaseZenReward(game.GameLevel, reward.Rank!.Value) * ZenMultiplier;
            }
        }
    }
}
