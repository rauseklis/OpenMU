// <copyright file="RibbonBoxRewards.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Configures exclusive item-or-Zen ribbon box rewards.
/// </summary>
public sealed class RibbonBoxRewards : InitializerBase
{
    private const byte BoxGroup = 12;
    private const double RedLuckChance = 0.25;
    private const double BlueSecondOptionChance = 0.15;

    /// <summary>
    /// Initializes a new instance of the <see cref="RibbonBoxRewards"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    public RibbonBoxRewards(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var red = this.ConfigureBox(32, 0.10, 10_000_000, [(0, 19), (2, 13), (4, 18), (5, 10), (5, 36)]);
        red.SkillChance = 1;
        red.LuckChance = RedLuckChance;
        this.ConfigureBox(33, 0.20, 5_000_000, [(12, 30), (12, 31), (12, 136), (12, 141)]);
        var blue = this.ConfigureBox(34, 0.15, 7_500_000, [(13, 8), (13, 9), (13, 12), (13, 13), (13, 21), (13, 22), (13, 23), (13, 25), (13, 26), (13, 27)]);
        blue.ItemType = SpecialItemType.Excellent;
        blue.ExcellentSecondOptionChance = BlueSecondOptionChance;
        blue.GuaranteeJewelryOption = true;
    }

    private ItemDropItemGroup ConfigureBox(short number, double itemChance, int zenAmount, (byte Group, short Number)[] rewards)
    {
        var box = this.GameConfiguration.Items.Single(item => item.Group == BoxGroup && item.Number == number);
        foreach (var oldGroup in box.DropItems.Where(group => group.SourceItemLevel == 0).ToArray())
        {
            box.DropItems.Remove(oldGroup);
        }

        var group = this.Context.CreateNew<ItemDropItemGroup>();
        group.Description = box.Name;
        group.ItemType = SpecialItemType.RandomItem;
        group.Chance = itemChance;
        group.DropEffect = ItemDropEffect.Fireworks;
        foreach (var reward in rewards)
        {
            group.PossibleItems.Add(this.GameConfiguration.Items.Single(item => item.Group == reward.Group && item.Number == reward.Number));
        }

        box.DropItems.Add(group);
        this.AddZenReward(box, 1 - itemChance, zenAmount);
        return group;
    }

    private void AddZenReward(MUnique.OpenMU.DataModel.Configuration.Items.ItemDefinition box, double chance, int amount)
    {
        var money = this.Context.CreateNew<ItemDropItemGroup>();
        money.Description = $"{box.Name} - Zen";
        money.ItemType = SpecialItemType.Money;
        money.Chance = chance;
        money.MoneyAmount = amount;
        money.DropEffect = ItemDropEffect.Fireworks;
        box.DropItems.Add(money);
    }
}
