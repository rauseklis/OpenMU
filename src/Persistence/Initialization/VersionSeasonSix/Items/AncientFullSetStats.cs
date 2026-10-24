// <copyright file="AncientFullSetStats.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Adds four full-set stat options while preserving existing Ancient options.
/// </summary>
internal static class AncientFullSetStats
{
    public static void AddFullSetStats(IContext context, GameConfiguration configuration, string name, float amount)
    {
        var set = configuration.ItemSetGroups.Single(group => group.Name == name);
        var options = set.Options ?? throw new InvalidOperationException($"Ancient set {name} has no options.");
        foreach (var attribute in new[] { Stats.TotalStrength, Stats.TotalAgility, Stats.TotalVitality, Stats.TotalEnergy })
        {
            if (options.PossibleOptions.Any(option => option.Number >= set.Items.Count
                && option.PowerUpDefinition is { TargetAttribute: { } target, Boost: { } boost }
                && target == attribute
                && boost.ConstantValue.Value == amount
                && boost.ConstantValue.AggregateType == AggregateType.AddRaw))
            {
                continue;
            }

            var option = context.CreateNew<IncreasableItemOption>();
            option.Number = options.PossibleOptions.Max(existing => existing.Number) + 1;
            option.OptionType = configuration.ItemOptionTypes.Single(type => type == ItemOptionTypes.AncientOption);
            option.PowerUpDefinition = CreateStatBonus(context, configuration, attribute, amount);
            options.PossibleOptions.Add(option);
        }
    }

    private static PowerUpDefinition CreateStatBonus(IContext context, GameConfiguration configuration, AttributeDefinition attribute, float amount)
    {
        var power = context.CreateNew<PowerUpDefinition>();
        power.TargetAttribute = attribute.GetPersistent(configuration);
        power.Boost = context.CreateNew<PowerUpDefinitionValue>();
        power.Boost.ConstantValue.Value = amount;
        power.Boost.ConstantValue.AggregateType = AggregateType.AddRaw;
        return power;
    }
}
