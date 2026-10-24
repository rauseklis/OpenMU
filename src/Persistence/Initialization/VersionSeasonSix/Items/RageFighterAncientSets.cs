// <copyright file="RageFighterAncientSets.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Items;

/// <summary>
/// Adds Chamer and Trite with the option order and item variants of the Season 6 client.
/// </summary>
public sealed class RageFighterAncientSets : AncientSets
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RageFighterAncientSets"/> class.
    /// </summary>
    public RageFighterAncientSets(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        this.AddChamer();
        this.AddTrite();
    }

    private void AddChamer()
    {
        if (this.GameConfiguration.ItemSetGroups.Any(set => set.Name == "Chamer"))
        {
            return;
        }

        var set = this.AddAncientSet("Chamer", 38,
            (Stats.DefenseBase, 50f, AggregateType.AddFinal),
            (Stats.DoubleDamageChance, 0.05f, AggregateType.AddRaw),
            (Stats.FinalDamageBonus, 30f, AggregateType.AddRaw),
            (Stats.CriticalDamageBonus, 30f, AggregateType.AddRaw),
            (Stats.ExcellentDamageChance, 0.15f, AggregateType.AddRaw),
            (Stats.SkillDamageBonus, 30f, AggregateType.AddRaw),
            (Stats.ExcellentDamageBonus, 20f, AggregateType.AddRaw));
        set.SetGuid(59, 2);
        this.AddItems(set,
            (32, ItemGroups.Swords, Stats.TotalStrength, 2),
            (59, ItemGroups.Armor, Stats.TotalVitality, 2),
            (59, ItemGroups.Pants, Stats.TotalVitality, 2),
            (59, ItemGroups.Boots, Stats.TotalVitality, 1));
    }

    private void AddTrite()
    {
        if (this.GameConfiguration.ItemSetGroups.Any(set => set.Name == "Trite"))
        {
            return;
        }

        var set = this.AddAncientSet("Trite", 49,
            (Stats.MaximumHealth, 50f, AggregateType.AddRaw),
            (Stats.DefenseBase, 40f, AggregateType.AddFinal),
            (Stats.DoubleDamageChance, 0.15f, AggregateType.AddRaw),
            (Stats.CriticalDamageChance, 0.15f, AggregateType.AddRaw),
            (Stats.ExcellentDamageChance, 0.15f, AggregateType.AddRaw),
            (Stats.CriticalDamageBonus, 20f, AggregateType.AddRaw),
            (Stats.ExcellentDamageBonus, 20f, AggregateType.AddRaw),
            (Stats.DefenseIgnoreChance, 0.05f, AggregateType.AddRaw));
        set.SetGuid(72, 1);
        this.AddItems(set,
            (72, ItemGroups.Helm, Stats.TotalVitality, 1),
            (72, ItemGroups.Armor, Stats.TotalVitality, 1),
            (72, ItemGroups.Pants, Stats.TotalVitality, 1),
            (72, ItemGroups.Boots, Stats.TotalVitality, 1));
    }
}
