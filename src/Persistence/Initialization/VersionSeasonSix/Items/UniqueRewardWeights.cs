// <copyright file="UniqueRewardWeights.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Removes equip-class multiplicity from global box reward weights.
/// </summary>
public sealed class UniqueRewardWeights(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    /// <inheritdoc />
    public override void Initialize()
    {
        var wrapper = this.GameConfiguration.Items.Single(item => item.Group == 14 && item.Number == 11);
        foreach (var level in new byte[] { 2, 5, 6, 8, 9, 10, 11, 12 })
        {
            this.NormalizeArmor(wrapper, level);
        }

        foreach (var level in new byte[] { 1, 8, 9, 10, 11, 12 })
        {
            this.NormalizeItems(wrapper, level, false);
            this.NormalizeItems(wrapper, level, true);
        }
    }

    private static void ReplaceGroups(ItemDefinition wrapper, ItemDropItemGroup[] old, ItemDropItemGroup[] replacement, double total)
    {
        // Put floating-point accumulation residue into the last allocation, not into another category.
        replacement[^1].Chance = total - replacement.Take(replacement.Length - 1).Sum(group => group.Chance);
        foreach (var group in old)
        {
            wrapper.DropItems.Remove(group);
        }

        foreach (var group in replacement)
        {
            wrapper.DropItems.Add(group);
        }
    }

    private static void EnsureSameRules(ItemDropItemGroup[] groups)
    {
        var first = groups[0];
        if (groups.Any(group => group.ItemType != first.ItemType || group.MinimumLevel != first.MinimumLevel
            || group.MaximumLevel != first.MaximumLevel || group.RequiredCharacterLevel != first.RequiredCharacterLevel
            || group.SkillChance != first.SkillChance || group.LuckChance != first.LuckChance
            || group.ExcellentSecondOptionChance != first.ExcellentSecondOptionChance || group.DropEffect != first.DropEffect
            || group.SuppressAdditionalOption != first.SuppressAdditionalOption || group.GuaranteeJewelryOption != first.GuaranteeJewelryOption
            || group.AncientSet != first.AncientSet || group.AncientBonusLevelTwoChance != first.AncientBonusLevelTwoChance
            || group.ItemLevel != first.ItemLevel || group.MinimumMonsterLevel != first.MinimumMonsterLevel
            || group.MaximumMonsterLevel != first.MaximumMonsterLevel || group.Monster != first.Monster || group.MoneyAmount != first.MoneyAmount))
        {
            throw new InvalidOperationException("Cannot merge reward routes with different item generation rules.");
        }
    }

    private void NormalizeArmor(ItemDefinition wrapper, byte level)
    {
        var groups = wrapper.DropItems.Where(group => group.SourceItemLevel == level && group.PossibleItems.Count > 0
            && group.PossibleItems.All(item => item.Group is >= 7 and <= 11)).ToArray();
        var sets = groups.SelectMany(group => group.PossibleItems).GroupBy(item => item.Number).OrderBy(set => set.Key).ToArray();
        if (sets.Length == 0)
        {
            return;
        }

        var total = groups.Sum(group => group.Chance);
        if (groups.Length == sets.Length && groups.All(group => group.PossibleItems.Select(item => item.Number).Distinct().Count() == 1
            && Math.Abs(group.Chance - (total / sets.Length)) < 1e-12))
        {
            return;
        }

        var replacement = sets.Select(set =>
        {
            var templates = groups.Where(group => group.PossibleItems.Any(item => item.Number == set.Key)).ToArray();
            EnsureSameRules(templates);
            return this.CreateGroup(templates[0], "armor", set.DistinctBy(item => (item.Group, item.Number)), total / sets.Length);
        }).ToArray();
        ReplaceGroups(wrapper, groups, replacement, total);
    }

    private void NormalizeItems(ItemDefinition wrapper, byte level, bool shields)
    {
        var groups = wrapper.DropItems.Where(group => group.SourceItemLevel == level && group.PossibleItems.Count > 0
            && group.PossibleItems.All(item => shields ? item.Group == 6 : item.Group < 6)).ToArray();
        var routes = groups.SelectMany(group => group.PossibleItems.Select(item => (Group: group, Item: item)))
            .GroupBy(route => (route.Item.Group, route.Item.Number)).OrderBy(item => item.Key).ToArray();
        if (routes.All(item => item.Count() == 1))
        {
            return;
        }

        var total = groups.Sum(group => group.Chance);

        // Averaging keeps original variant weights, without rewarding repeated class references.
        var weights = routes.Select(item => item.Average(route => route.Group.Chance / route.Group.PossibleItems.Count)).ToArray();
        var weightSum = weights.Sum();
        var replacement = routes.Select((item, index) =>
        {
            EnsureSameRules(item.Select(route => route.Group).ToArray());
            var first = item.First();
            return this.CreateGroup(first.Group, shields ? "shields" : "weapons", [first.Item], total * weights[index] / weightSum);
        }).ToArray();
        ReplaceGroups(wrapper, groups, replacement, total);
    }

    private ItemDropItemGroup CreateGroup(ItemDropItemGroup source, string category, IEnumerable<ItemDefinition> items, double chance)
    {
        var result = this.Context.CreateNew<ItemDropItemGroup>();
        result.Description = $"Unique rewards: {source.SourceItemLevel} / {category} / {items.First().Name}";
        result.SourceItemLevel = source.SourceItemLevel;
        result.Chance = chance;
        result.ItemType = source.ItemType;
        result.MinimumLevel = source.MinimumLevel;
        result.MaximumLevel = source.MaximumLevel;
        result.RequiredCharacterLevel = source.RequiredCharacterLevel;
        result.DropEffect = source.DropEffect;
        result.SkillChance = source.SkillChance;
        result.LuckChance = source.LuckChance;
        result.ExcellentSecondOptionChance = source.ExcellentSecondOptionChance;
        result.GuaranteeJewelryOption = source.GuaranteeJewelryOption;
        result.SuppressAdditionalOption = source.SuppressAdditionalOption;
        result.AncientSet = source.AncientSet;
        result.AncientBonusLevelTwoChance = source.AncientBonusLevelTwoChance;
        result.MinimumMonsterLevel = source.MinimumMonsterLevel;
        result.MaximumMonsterLevel = source.MaximumMonsterLevel;
        result.Monster = source.Monster;
        result.ItemLevel = source.ItemLevel;
        result.MoneyAmount = source.MoneyAmount;
        foreach (var item in items.OrderBy(item => item.Group).ThenBy(item => item.Number))
        {
            result.PossibleItems.Add(item);
        }

        return result;
    }
}
