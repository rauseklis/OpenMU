// <copyright file="RibbonBoxRewards.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Configures guaranteed ribbon box rewards at +0.
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
        var red = this.ConfigureBox(32, [(0, 19), (2, 13), (4, 18), (5, 10), (5, 36)]);
        red.SkillChance = 1;
        red.LuckChance = RedLuckChance;
        this.ConfigureBox(33, [(12, 30), (12, 31), (12, 136), (12, 141)]);
        var blue = this.ConfigureBox(34, [(13, 8), (13, 9), (13, 12), (13, 13), (13, 21), (13, 22), (13, 23), (13, 25), (13, 26), (13, 27)]);
        blue.ItemType = SpecialItemType.Excellent;
        blue.ExcellentSecondOptionChance = BlueSecondOptionChance;
        blue.GuaranteeJewelryOption = true;
    }

    private ItemDropItemGroup ConfigureBox(short number, (byte Group, short Number)[] rewards)
    {
        var box = this.GameConfiguration.Items.Single(item => item.Group == BoxGroup && item.Number == number);
        foreach (var oldGroup in box.DropItems.Where(group => group.SourceItemLevel == 0).ToArray())
        {
            box.DropItems.Remove(oldGroup);
        }

        var group = this.Context.CreateNew<ItemDropItemGroup>();
        group.Description = box.Name;
        group.ItemType = SpecialItemType.RandomItem;
        group.Chance = 1;
        group.DropEffect = ItemDropEffect.Fireworks;
        foreach (var reward in rewards)
        {
            group.PossibleItems.Add(this.GameConfiguration.Items.Single(item => item.Group == reward.Group && item.Number == reward.Number));
        }

        box.DropItems.Add(group);
        return group;
    }
}
