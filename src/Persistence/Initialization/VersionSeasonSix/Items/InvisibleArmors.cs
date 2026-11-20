// <copyright file="InvisibleArmors.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Items;

/// <summary>
/// Adds the Invisible set: end-game helm, armor, pants, gloves and boots which the client draws as missing (+0 to +6) or as the
/// character's own body with a golden glow (+7 and higher).
/// </summary>
/// <remarks>
/// Every piece has 200 defense at every item level, needs 100 total strength, agility, vitality and energy at every item level
/// and has no options. The gloves add 100 attack speed. The pieces don't drop from monsters; they are given by a game master.
/// </remarks>
public sealed class InvisibleArmors : InitializerBase
{
    /// <summary>
    /// The item number of every Invisible piece in its group.
    /// </summary>
    public const short Number = 74;

    /// <summary>
    /// The defense of every piece, independent of the item level.
    /// </summary>
    public const int Defense = 200;

    /// <summary>
    /// The total strength, agility, vitality and energy needed to wear a piece, independent of the item level.
    /// </summary>
    public const int StatRequirement = 100;

    /// <summary>
    /// The attack speed which the gloves add.
    /// </summary>
    public const int GlovesAttackSpeed = 100;

    private const byte DropLevel = 100;
    private const byte Durability = 90;
    private const byte MaximumLevel = 15;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvisibleArmors"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public InvisibleArmors(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        this.AddPiece(ItemGroups.Helm, 2, "Invisible Helm", 2, 2, magicGladiator: false, rageFighter: true);
        this.AddPiece(ItemGroups.Armor, 3, "Invisible Armor", 2, 3, magicGladiator: true, rageFighter: true);
        this.AddPiece(ItemGroups.Pants, 4, "Invisible Pants", 2, 2, magicGladiator: true, rageFighter: true);
        var gloves = this.AddPiece(ItemGroups.Gloves, 5, "Invisible Gloves", 2, 2, magicGladiator: true, rageFighter: false);
        if (gloves is not null)
        {
            gloves.BasePowerUpAttributes.Add(this.CreateItemBasePowerUpDefinition(Stats.AttackSpeedAny, GlovesAttackSpeed, AggregateType.AddRaw));
        }

        this.AddPiece(ItemGroups.Boots, 6, "Invisible Boots", 2, 2, magicGladiator: true, rageFighter: true);
    }

    private ItemDefinition? AddPiece(ItemGroups group, byte slot, string name, byte width, byte height, bool magicGladiator, bool rageFighter)
    {
        if (this.GameConfiguration.Items.Any(item => item.Group == (byte)group && item.Number == Number))
        {
            return null;
        }

        var item = this.Context.CreateNew<ItemDefinition>();
        item.Group = (byte)group;
        item.Number = Number;
        item.SetGuid(item.Group, item.Number);
        item.Name = name;
        item.Width = width;
        item.Height = height;
        item.DropLevel = DropLevel;
        item.Durability = Durability;
        item.MaximumItemLevel = MaximumLevel;
        item.DropsFromMonsters = false;
        item.ItemSlot = this.GameConfiguration.ItemSlotTypes.First(type => type.ItemSlots.Contains(slot));

        // The total stats (not the "requirement values") keep the requirement at 100 for every item level.
        this.CreateItemRequirementIfNeeded(item, Stats.TotalStrength, StatRequirement);
        this.CreateItemRequirementIfNeeded(item, Stats.TotalAgility, StatRequirement);
        this.CreateItemRequirementIfNeeded(item, Stats.TotalVitality, StatRequirement);
        this.CreateItemRequirementIfNeeded(item, Stats.TotalEnergy, StatRequirement);

        // No level bonus table: the defense stays at 200 for every item level.
        item.BasePowerUpAttributes.Add(this.CreateItemBasePowerUpDefinition(Stats.DefenseBase, Defense, AggregateType.AddRaw));

        var classes = this.GameConfiguration.DetermineCharacterClasses(1, 1, 1, magicGladiator ? 1 : 0, 1, 1, rageFighter ? 1 : 0);
        foreach (var characterClass in classes)
        {
            item.QualifiedCharacters.Add(characterClass);
        }

        this.GameConfiguration.Items.Add(item);
        return item;
    }
}
