// <copyright file="ArgoSpiritTierTwo.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Replaces Gaia's added Tier 2 bonus and Silver Box rewards with Argo Spirit.
/// </summary>
public sealed class ArgoSpiritTierTwo(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    private const int GaiaOriginalOptionCount = 7;
    private const float FullSetBonus = 50;

    /// <inheritdoc />
    public override void Initialize()
    {
        var gaia = this.GameConfiguration.ItemSetGroups.Single(set => set.Name == "Gaia");
        var stats = new[] { Stats.TotalStrength, Stats.TotalAgility, Stats.TotalVitality, Stats.TotalEnergy };
        foreach (var option in gaia.Options!.PossibleOptions.Where(option => option.Number > GaiaOriginalOptionCount
            && option.PowerUpDefinition is { TargetAttribute: { } target, Boost: { } boost }
            && stats.Contains(target)
            && boost.ConstantValue.Value == FullSetBonus
            && boost.ConstantValue.AggregateType == AggregateType.AddRaw).ToList())
        {
            gaia.Options.PossibleOptions.Remove(option);
        }

        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Argo", FullSetBonus);
        new AncientRewardBoxes(this.Context, this.GameConfiguration).InitializeSilverBox();
    }
}
