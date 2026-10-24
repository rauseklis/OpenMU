// <copyright file="BoxRewardOptions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Applies optional box reward rules without changing shared Luck and equipment ADD rules.
/// </summary>
public static class BoxRewardOptions
{
    private const byte MinimumJewelryOptionLevel = 1;
    private const byte MaximumJewelryOptionLevel = 3;
    private const byte JewelryGroup = 13;

    /// <summary>
    /// Applies the reward group's optional rules to an already generated item.
    /// </summary>
    /// <param name="item">The generated item.</param>
    /// <param name="group">The selected reward group.</param>
    /// <param name="randomizer">The randomizer.</param>
    public static void Apply(Item item, ItemDropItemGroup group, IRandomizer randomizer)
    {
        ApplyLuck(item, group, randomizer);
        ApplyExcellentOptions(item, group, randomizer);
        if (group.ItemType == SpecialItemType.Excellent)
        {
            item.HasSkill = item.CanHaveSkill();
        }
        else if (group.SkillChance is { } skillChance)
        {
            ValidateChance(skillChance);
            item.HasSkill = item.CanHaveSkill() && randomizer.NextDouble() < skillChance;
        }

        if (group.GuaranteeJewelryOption)
        {
            ApplyJewelryOption(item, randomizer);
        }
    }

    private static void ApplyLuck(Item item, ItemDropItemGroup group, IRandomizer randomizer)
    {
        if (group.LuckChance is not { } chance)
        {
            return;
        }

        ValidateChance(chance);
        var luck = item.Definition!.PossibleItemOptions.SelectMany(options => options.PossibleOptions)
            .FirstOrDefault(option => Equals(option.OptionType, ItemOptionTypes.Luck));
        foreach (var link in item.ItemOptions.Where(link => Equals(link.ItemOption?.OptionType, ItemOptionTypes.Luck)).ToArray())
        {
            item.ItemOptions.Remove(link);
        }

        if (luck is not null && randomizer.NextDouble() < chance)
        {
            item.ItemOptions.Add(new ItemOptionLink { ItemOption = luck });
        }
    }

    private static void ApplyExcellentOptions(Item item, ItemDropItemGroup group, IRandomizer randomizer)
    {
        if (group.ItemType != SpecialItemType.Excellent || group.ExcellentSecondOptionChance is not { } chance)
        {
            return;
        }

        ValidateChance(chance);
        var options = item.Definition!.PossibleItemOptions
            .SelectMany(definition => definition.PossibleOptions)
            .Where(option => Equals(option.OptionType, ItemOptionTypes.Excellent))
            .Distinct().ToList();
        if (options.Count == 0)
        {
            throw new InvalidOperationException("Excellent box reward has no supported Excellent options.");
        }

        foreach (var link in item.ItemOptions.Where(link => Equals(link.ItemOption?.OptionType, ItemOptionTypes.Excellent)).ToList())
        {
            item.ItemOptions.Remove(link);
        }

        var count = options.Count > 1 && randomizer.NextDouble() < chance ? 2 : 1;
        for (var index = 0; index < count; index++)
        {
            var selected = randomizer.NextInt(0, options.Count);
            item.ItemOptions.Add(new ItemOptionLink { ItemOption = options[selected] });
            options.RemoveAt(selected);
        }
    }

    private static void ApplyJewelryOption(Item item, IRandomizer randomizer)
    {
        var definition = item.Definition!;
        var option = definition.PossibleItemOptions.SelectMany(options => options.PossibleOptions)
            .FirstOrDefault(option => Equals(option.OptionType, ItemOptionTypes.Option)
                && (option.PowerUpDefinition?.TargetAttribute == Stats.HealthRecoveryMultiplier
                    || option.PowerUpDefinition?.TargetAttribute == Stats.MaximumMana));
        if (definition.Group != JewelryGroup || option is null)
        {
            throw new InvalidOperationException("Jewelry box reward has no supported HP recovery or maximum-mana option.");
        }

        var level = (byte)randomizer.NextInt(MinimumJewelryOptionLevel, MaximumJewelryOptionLevel + 1);
        var link = item.ItemOptions.FirstOrDefault(link => Equals(link.ItemOption, option));
        if (link is null)
        {
            item.ItemOptions.Add(new ItemOptionLink { ItemOption = option, Level = level });
            return;
        }

        link.Level = level;
    }

    private static void ValidateChance(double chance)
    {
        if (!double.IsFinite(chance) || chance is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chance), "Box reward chances must be between zero and one.");
        }
    }
}
