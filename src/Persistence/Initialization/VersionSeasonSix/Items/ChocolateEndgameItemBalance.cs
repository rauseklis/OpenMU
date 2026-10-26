// <copyright file="ChocolateEndgameItemBalance.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Updates existing custom maces and final Summoner requirements without replacing definitions.
/// </summary>
public sealed class ChocolateEndgameItemBalance : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChocolateEndgameItemBalance"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The selected configuration.</param>
    public ChocolateEndgameItemBalance(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        foreach (var (number, minimum, maximum) in new[] { (22, 150, 165), (23, 200, 220) })
        {
            var item = this.GameConfiguration.Items.Single(item => item.Group == 2 && item.Number == number);
            item.BasePowerUpAttributes.Single(power => power.TargetAttribute == Stats.MinimumPhysBaseDmgByWeapon).BaseValue = minimum;
            item.BasePowerUpAttributes.Single(power => power.TargetAttribute == Stats.MaximumPhysBaseDmgByWeapon).BaseValue = maximum;
        }

        foreach (var (group, strength, agility) in new[] { (7, 64, 22), (8, 70, 24), (9, 72, 24), (10, 72, 24), (11, 72, 24) })
        {
            var item = this.GameConfiguration.Items.Single(item => item.Group == group && item.Number == 44);
            item.Requirements.Single(requirement => requirement.Attribute == Stats.TotalStrengthRequirementValue).MinimumValue = strength;
            item.Requirements.Single(requirement => requirement.Attribute == Stats.TotalAgilityRequirementValue).MinimumValue = agility;
        }

        var stick = this.GameConfiguration.Items.Single(item => item.Group == 5 && item.Number == 20);
        stick.BasePowerUpAttributes.Single(power => power.TargetAttribute == Stats.StaffRise).BaseValue = 65;
        foreach (var requirement in stick.Requirements.Where(requirement => requirement.Attribute == Stats.Level).ToArray())
        {
            stick.Requirements.Remove(requirement);
        }
    }
}
