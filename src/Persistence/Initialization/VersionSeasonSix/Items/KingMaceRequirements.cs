// <copyright file="KingMaceRequirements.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Applies the approved rare mace requirements to new and existing configurations.
/// </summary>
public sealed class KingMaceRequirements : InitializerBase
{
    public KingMaceRequirements(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    public override void Initialize()
    {
        foreach (var entry in new[] { (Number: 22, Strength: 480, Agility: 334), (Number: 23, Strength: 968, Agility: 675) })
        {
            var item = this.GameConfiguration.Items.Single(i => i.Group == 2 && i.Number == entry.Number);
            this.SetRequirement(item, Stats.TotalStrengthRequirementValue, entry.Strength);
            this.SetRequirement(item, Stats.TotalAgilityRequirementValue, entry.Agility);
            item.Skill = null;
        }
    }

    private void SetRequirement(ItemDefinition item, AttributeDefinition attribute, int coefficient)
    {
        var requirement = item.Requirements.SingleOrDefault(r => r.Attribute == attribute);
        if (requirement is null)
        {
            this.CreateItemRequirementIfNeeded(item, attribute, coefficient);
            return;
        }

        requirement.MinimumValue = coefficient;
    }
}
