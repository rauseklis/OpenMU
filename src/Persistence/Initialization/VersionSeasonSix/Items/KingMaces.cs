// <copyright file="KingMaces.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Adds staged one-handed king maces without changing ordinary weapon tables.
/// </summary>
public sealed class KingMaces : InitializerBase
{
    private const string BonusTableName = "King Maces Damage Increase";

    /// <summary>
    /// Initializes a new instance of the <see cref="KingMaces"/> class.
    /// </summary>
    public KingMaces(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        var template = this.GameConfiguration.Items.Single(i => i.Group == 0 && i.Number == 16);
        foreach (var entry in new[] { (Number: (short)22, Name: "Mace of King", Min: 110, Max: 125, Speed: 30),
                     (Number: (short)23, Name: "Royal Mace of King", Min: 150, Max: 170, Speed: 40) })
        {
            if (this.GameConfiguration.Items.Any(i => i.Group == 2 && i.Number == entry.Number))
            {
                var existing = this.GameConfiguration.Items.Single(i => i.Group == 2 && i.Number == entry.Number);
                if (existing.Name.ToString() != entry.Name)
                {
                    throw new InvalidOperationException($"Mace slot 2/{entry.Number} is already occupied by {existing.Name}.");
                }

                continue;
            }

            var table = this.GameConfiguration.ItemLevelBonusTables.SingleOrDefault(t => t.Name.ToString() == BonusTableName)
                ?? this.CreateItemBonusTable(Enumerable.Range(0, 16).Select(level => level * 4f).ToArray(), BonusTableName, "Adds four minimum and maximum damage per upgrade level.");
            var item = this.Context.CreateNew<ItemDefinition>();
            item.Group = 2;
            item.Number = entry.Number;
            item.Name = entry.Name;
            item.SetGuid(item.Group, item.Number);
            item.Width = 1;
            item.Height = 3;
            item.ItemSlot = template.ItemSlot;
            item.DropLevel = template.DropLevel;
            item.Durability = template.Durability;
            item.MaximumItemLevel = template.MaximumItemLevel;
            item.DropsFromMonsters = false;
            item.IsTradable = true;
            item.IsDroppable = true;
            item.IsStorable = true;
            item.IsRepairable = true;
            item.IsSellableToNpc = true;
            item.IsPersonalStoreSellable = true;
            foreach (var cls in this.GameConfiguration.CharacterClasses.Where(c => c.Number / 4 is 1 or 3 or 4))
            {
                item.QualifiedCharacters.Add(cls);
            }

            foreach (var requirement in template.Requirements)
            {
                var copy = this.Context.CreateNew<AttributeRequirement>();
                copy.Attribute = requirement.Attribute;
                copy.MinimumValue = requirement.MinimumValue;
                item.Requirements.Add(copy);
            }

            foreach (var option in template.PossibleItemOptions)
            {
                item.PossibleItemOptions.Add(option);
            }

            var min = this.CreateItemBasePowerUpDefinition(Stats.MinimumPhysBaseDmgByWeapon, entry.Min, AggregateType.AddRaw);
            var max = this.CreateItemBasePowerUpDefinition(Stats.MaximumPhysBaseDmgByWeapon, entry.Max, AggregateType.AddRaw);
            min.BonusPerLevelTable = table;
            max.BonusPerLevelTable = table;
            item.BasePowerUpAttributes.Add(min);
            item.BasePowerUpAttributes.Add(max);
            item.BasePowerUpAttributes.Add(this.CreateItemBasePowerUpDefinition(Stats.AttackSpeedByWeapon, entry.Speed, AggregateType.AddRaw));
            item.BasePowerUpAttributes.Add(this.CreateItemBasePowerUpDefinition(Stats.EquippedWeaponCount, 1, AggregateType.AddRaw));
            item.BasePowerUpAttributes.Add(this.CreateItemBasePowerUpDefinition(Stats.DoubleWieldWeaponCount, 1, AggregateType.AddRaw));
            this.GameConfiguration.Items.Add(item);
        }

        new KingMaceRequirements(this.Context, this.GameConfiguration).Initialize();
    }
}
