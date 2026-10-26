// <copyright file="ItemDropItemGroup.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration;

using MUnique.OpenMU.Annotations;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Defines an effect which is shown in the game, when a <see cref="ItemDropItemGroup"/> is applied.
/// </summary>
public enum ItemDropEffect
{
    /// <summary>
    /// No effect.
    /// </summary>
    Undefined,

    /// <summary>
    /// A fireworks is shown at specific coordinates.
    /// </summary>
    Fireworks,

    /// <summary>
    /// A christmas fireworks is shown at specific coordinates.
    /// </summary>
    ChristmasFireworks,

    /// <summary>
    /// A fanfare sound is played.
    /// </summary>
    FanfareSound,

    /// <summary>
    /// A swirl is shown around an object, usually the player which caused the action.
    /// </summary>
    Swirl,
}

/// <summary>
/// A <see cref="DropItemGroup"/> which acts a definition of possible items when a special item (e.g. Box of Luck) is dropped by the player.
/// </summary>
[Cloneable]
public partial class ItemDropItemGroup : DropItemGroup
{
    /// <summary>
    /// Gets or sets the <see cref="Item.Level"/> of the source item which was dropped by the player.
    /// </summary>
    public byte SourceItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the amount of money, in case <see cref="DropItemGroup.ItemType"/> is <see cref="SpecialItemType.Money"/>.
    /// </summary>
    public int MoneyAmount { get; set; }

    /// <summary>
    /// Gets or sets the minimum level of the <see cref="DropItemGroup.PossibleItems"/>.
    /// </summary>
    public byte MinimumLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum level of the <see cref="DropItemGroup.PossibleItems"/>.
    /// </summary>
    public byte MaximumLevel { get; set; }

    /// <summary>
    /// Gets or sets the character level which is required to drop this item.
    /// </summary>
    public short RequiredCharacterLevel { get; set; }

    /// <summary>
    /// Gets or sets the effect which should be shown in the game client when this drop group is applied.
    /// </summary>
    public ItemDropEffect DropEffect { get; set; }

    /// <summary>
    /// Gets or sets the chance for a second Excellent option. Null preserves shared rules.
    /// When set, an Excellent reward receives exactly one or two distinct supported options.
    /// </summary>
    public double? ExcellentSecondOptionChance { get; set; }

    /// <summary>
    /// Gets or sets the supported skill chance for ordinary rewards. Null preserves shared rules.
    /// Excellent rewards always retain their supported skill.
    /// </summary>
    public double? SkillChance { get; set; }

    /// <summary>
    /// Gets or sets the luck chance for this box reward. Null preserves shared rules.
    /// </summary>
    public double? LuckChance { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether jewelry rewards always receive a supported HP recovery or
    /// maximum-mana option at an equally selected level from one through three.
    /// </summary>
    public bool GuaranteeJewelryOption { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether ordinary equipment ADD is removed from this reward.
    /// Shared item definitions and other reward sources retain their existing options.
    /// </summary>
    public bool SuppressAdditionalOption { get; set; }

    /// <summary>
    /// Gets or sets the required Ancient set of this reward group. Null preserves random set selection.
    /// </summary>
    public virtual ItemSetGroup? AncientSet { get; set; }

    /// <summary>
    /// Gets or sets the chance for the +10 Ancient stat bonus instead of +5. Null preserves shared rules.
    /// </summary>
    public double? AncientBonusLevelTwoChance { get; set; }
}
