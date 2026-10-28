// <copyright file="ChaosCastleBoxRewards.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>Replaces only Chaos Castle's Ancient equipment reward with the approved existing boxes.</summary>
public sealed class ChaosCastleBoxRewards(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    private const byte BoxGroup = 14;
    private const short LuckBox = 11;
    private const short SilverBox = 124;
    private const short GoldenBox = 123;

    /// <inheritdoc />
    public override void Initialize()
    {
        foreach (var game in this.GameConfiguration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.ChaosCastle && game.GameLevel is >= 1 and <= 7))
        {
            this.ConfigureAncientReward(game);
        }
    }

    private void ConfigureAncientReward(MiniGameDefinition game)
    {
        var description = $"Rewarded Ancient boxes for Chaos Castle {game.GameLevel}";
        var previous = game.Rewards.Where(reward => reward.ItemReward is { } group
            && (group.ItemType == SpecialItemType.Ancient || group.Description == description)).ToArray();
        foreach (var duplicate in previous.Skip(game.GameLevel == 1 ? 0 : 1))
        {
            game.Rewards.Remove(duplicate);
        }

        // An absent entry is essential: the generic reward service treats Chance=0 as legacy guaranteed delivery.
        if (game.GameLevel == 1)
        {
            return;
        }

        var reward = previous.FirstOrDefault() ?? this.Context.CreateNew<MiniGameReward>();
        var group = reward.ItemReward ?? this.Context.CreateNew<DropItemGroup>();
        group.Description = description;
        group.ItemType = SpecialItemType.RandomItem;
        group.ItemLevel = 0;
        group.Chance = game.GameLevel switch { 2 => .05, 3 => .10, 4 => .15, 5 => .20, 6 => .25, _ => .30 };
        group.PossibleItems.Clear();
        var numbers = game.GameLevel switch
        {
            <= 4 => new[] { LuckBox },
            5 => new[] { LuckBox, SilverBox },
            6 => new[] { SilverBox },
            _ => new[] { SilverBox, GoldenBox },
        };
        foreach (var number in numbers)
        {
            group.PossibleItems.Add(this.GameConfiguration.Items.Single(item => item.Group == BoxGroup && item.Number == number));
        }

        reward.ItemReward = group;
        reward.RewardAmount = 1;
        reward.RewardType = MiniGameRewardType.ItemDrop;
        reward.RequiredSuccess = MiniGameSuccessFlags.Winner | MiniGameSuccessFlags.Alive;
        if (!this.GameConfiguration.DropItemGroups.Contains(group))
        {
            this.GameConfiguration.DropItemGroups.Add(group);
        }

        if (!game.Rewards.Contains(reward))
        {
            game.Rewards.Add(reward);
        }
    }
}
