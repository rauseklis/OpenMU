// <copyright file="RewardCategoryRatios.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Scales equipment categories while retaining their normalized internal weights.
/// </summary>
public sealed class RewardCategoryRatios(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    /// <inheritdoc />
    public override void Initialize()
    {
        var wrapper = this.FindBox(11);
        for (byte level = 8; level <= 12; level++)
        {
            Configure(wrapper, level, .70, .20, .10);
        }

        Configure(wrapper, 5, .4125, .11, .0275);
        Configure(wrapper, 6, .525, .14, .035);
        Configure(wrapper, 2, .32, .08, 0);
        Configure(wrapper, 1, .75, .20, .05);
        Configure(this.FindBox(32), 0, .70, .20, .10);
        Configure(this.FindBox(34), 0, .80, .15, 0);
    }

    private static void Configure(ItemDefinition box, byte level, double armor, double weapons, double shields)
    {
        Scale(box, level, item => item.Group is >= 7 and <= 11, armor);
        Scale(box, level, item => item.Group < 6 && !(item.Group == 2 && item.Number == 22), weapons);
        Scale(box, level, item => item.Group == 6, shields);
    }

    private static void Scale(ItemDefinition box, byte level, Func<ItemDefinition, bool> selected, double target)
    {
        var groups = box.DropItems.Where(group => group.SourceItemLevel == level && group.PossibleItems.Count > 0
            && group.PossibleItems.All(selected)).ToArray();
        var current = groups.Sum(group => group.Chance);
        if (Math.Abs(current - target) < 1e-12)
        {
            return;
        }

        if (current <= 0)
        {
            throw new InvalidOperationException($"Missing equipment category in box {box.Number}, source level {level}.");
        }

        var factor = target / current;
        foreach (var group in groups)
        {
            group.Chance *= factor;
        }

        // Keep category rounding residue in this category, without changing another outcome.
        groups[^1].Chance = target - groups.Take(groups.Length - 1).Sum(group => group.Chance);
    }

    private ItemDefinition FindBox(short number) => this.GameConfiguration.Items.Single(item => item.Group == 14 && item.Number == number);
}
