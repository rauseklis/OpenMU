// <copyright file="TriteArmors.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Adds permanent Storm Jahad items at the client's Trite item numbers.
/// </summary>
public sealed class TriteArmors : InitializerBase
{
    private const short TriteNumber = 72;
    private const short OrdinaryStormNumber = 60;
    private const int RequiredLevel = 150;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriteArmors"/> class.
    /// </summary>
    public TriteArmors(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        foreach (var entry in new[] { (Group: (byte)7, Name: "Helm", Defense: 32),
                     (Group: (byte)8, Name: "Armor", Defense: 43),
                     (Group: (byte)9, Name: "Pants", Defense: 33),
                     (Group: (byte)11, Name: "Boots", Defense: 20) })
        {
            this.AddArmor(entry.Group, entry.Name, entry.Defense);
        }

        this.AddOrdinarySetBonuses();
    }

    private void AddArmor(byte group, string name, int defense)
    {
        if (this.GameConfiguration.Items.Any(item => item.Group == group && item.Number == TriteNumber))
        {
            return;
        }

        var template = this.GameConfiguration.Items.Single(item => item.Group == group && item.Number == OrdinaryStormNumber);
        var item = this.Context.CreateNew<ItemDefinition>();
        item.Group = group;
        item.Number = TriteNumber;
        item.Name = $"Storm Jahad {name}";
        item.SetGuid(group, TriteNumber);
        item.Width = template.Width;
        item.Height = template.Height;
        item.ItemSlot = template.ItemSlot;
        item.DropLevel = template.DropLevel;
        item.Durability = byte.MaxValue;
        item.MaximumItemLevel = template.MaximumItemLevel;
        item.DropsFromMonsters = false;
        this.CreateItemRequirementIfNeeded(item, Stats.Level, RequiredLevel);
        foreach (var characterClass in template.QualifiedCharacters)
        {
            item.QualifiedCharacters.Add(characterClass);
        }

        foreach (var option in template.PossibleItemOptions)
        {
            item.PossibleItemOptions.Add(option);
        }

        foreach (var source in template.BasePowerUpAttributes)
        {
            var copy = this.Context.CreateNew<ItemBasePowerUpDefinition>();
            copy.TargetAttribute = source.TargetAttribute;
            copy.AggregateType = source.AggregateType;
            copy.BonusPerLevelTable = source.BonusPerLevelTable;
            copy.BaseValue = source.TargetAttribute == Stats.DefenseBase ? defense : source.BaseValue;
            item.BasePowerUpAttributes.Add(copy);
        }

        this.GameConfiguration.Items.Add(item);
    }

    private void AddOrdinarySetBonuses()
    {
        var templates = this.GameConfiguration.Items.Single(item => item.Group == 7 && item.Number == OrdinaryStormNumber)
            .PossibleItemSetGroups.Where(set => set.AlwaysApplies).ToList();
        var items = this.GameConfiguration.Items.Where(item => item.Number == TriteNumber && item.Group is 7 or 8 or 9 or 11).ToList();
        foreach (var template in templates)
        {
            if (items.Any(item => item.PossibleItemSetGroups.Any(set => set.AlwaysApplies && set.SetLevel == template.SetLevel)))
            {
                continue;
            }

            var set = this.Context.CreateNew<ItemSetGroup>();
            set.Name = $"Trite ordinary armor bonus (Level {template.SetLevel})";
            set.AlwaysApplies = true;
            set.CountDistinct = template.CountDistinct;
            set.MinimumItemCount = items.Count;
            set.SetLevel = template.SetLevel;
            set.Options = template.Options;
            foreach (var item in items)
            {
                var member = this.Context.CreateNew<ItemOfItemSet>();
                member.SetGuid(item.Group, item.Number, template.SetLevel == 0 ? byte.MaxValue : (byte)template.SetLevel);
                member.ItemDefinition = item;
                member.ItemSetGroup = set;
                set.Items.Add(member);
                item.PossibleItemSetGroups.Add(set);
            }

            this.GameConfiguration.ItemSetGroups.Add(set);
        }
    }
}
