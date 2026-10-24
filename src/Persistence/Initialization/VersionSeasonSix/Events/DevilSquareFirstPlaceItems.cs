// <copyright file="DevilSquareFirstPlaceItems.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Gives only first place the matching BC item reward in DS levels one through six.
/// </summary>
public sealed class DevilSquareFirstPlaceItems : InitializerBase
{
    private const byte LastItemRewardLevel = 6;
    private const int WinningRank = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevilSquareFirstPlaceItems"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    public DevilSquareFirstPlaceItems(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        foreach (var square in this.GameConfiguration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.DevilSquare && game.GameLevel is >= 1 and <= LastItemRewardLevel))
        {
            this.ConfigureItems(square);
        }
    }

    private void ConfigureItems(MiniGameDefinition square)
    {
        var castle = this.GameConfiguration.MiniGameDefinitions.Single(game => game.Type == MiniGameType.BloodCastle && game.GameLevel == square.GameLevel);
        var castleItems = castle.Rewards.Single(reward => reward.RewardType == MiniGameRewardType.Item);
        foreach (var previous in square.Rewards.Where(reward => reward.RewardType is MiniGameRewardType.Item or MiniGameRewardType.ItemDrop).ToArray())
        {
            square.Rewards.Remove(previous);
        }

        var items = this.Context.CreateNew<MiniGameReward>();
        items.Rank = WinningRank;
        items.RewardType = MiniGameRewardType.Item;
        items.RewardAmount = castleItems.RewardAmount;
        items.ItemReward = castleItems.ItemReward;
        square.Rewards.Add(items);
    }
}
