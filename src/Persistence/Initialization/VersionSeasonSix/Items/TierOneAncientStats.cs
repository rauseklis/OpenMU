// <copyright file="TierOneAncientStats.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Adds the approved full-set stats to low-tier Ancient equipment.
/// </summary>
public sealed class TierOneAncientStats : InitializerBase
{
    private const float StandardBonus = 100;
    private const float BrassBonus = 75;

    /// <summary>
    /// Initializes a new instance of the <see cref="TierOneAncientStats"/> class.
    /// </summary>
    public TierOneAncientStats(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        this.AddFullSetStats("Hyperion", StandardBonus);
        this.AddFullSetStats("Barnake", StandardBonus);
        this.AddFullSetStats("Fase", StandardBonus);
        this.AddFullSetStats("Cloud", BrassBonus);
    }

    private void AddFullSetStats(string name, float amount)
    {
        var set = this.GameConfiguration.ItemSetGroups.Single(group => group.Name == name);
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

            var option = this.Context.CreateNew<IncreasableItemOption>();
            option.Number = options.PossibleOptions.Max(existing => existing.Number) + 1;
            option.OptionType = this.GameConfiguration.ItemOptionTypes.Single(type => type == ItemOptionTypes.AncientOption);
            option.PowerUpDefinition = this.CreateStatBonus(attribute, amount);
            options.PossibleOptions.Add(option);
        }
    }

    private PowerUpDefinition CreateStatBonus(AttributeDefinition attribute, float amount)
    {
        var power = this.Context.CreateNew<PowerUpDefinition>();
        power.TargetAttribute = attribute.GetPersistent(this.GameConfiguration);
        power.Boost = this.Context.CreateNew<PowerUpDefinitionValue>();
        power.Boost.ConstantValue.Value = amount;
        power.Boost.ConstantValue.AggregateType = AggregateType.AddRaw;
        return power;
    }
}
