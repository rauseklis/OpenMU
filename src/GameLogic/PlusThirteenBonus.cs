// <copyright file="PlusThirteenBonus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// The bonuses which are given for wearing +13 items:
/// a complete armor set of the same kind (all pieces at least +13) gives a set bonus,
/// and a weapon at least +13 gives a chance for a stronger critical strike.
/// </summary>
public static class PlusThirteenBonus
{
    /// <summary>
    /// The minimum item level which is required for the bonuses.
    /// </summary>
    public const byte RequiredLevel = 13;

    /// <summary>
    /// The vitality which is added by the complete +13 set.
    /// </summary>
    public const float SetVitality = 100;

    /// <summary>
    /// The chance to ignore the defense of the target which is added by the complete +13 set (5%).
    /// </summary>
    public const float SetDefenseIgnoreChance = 0.05f;

    /// <summary>
    /// The chance of the +13 weapon strike (2%).
    /// </summary>
    public const float WeaponStrikeChance = 0.02f;

    /// <summary>
    /// The damage multiplier of the +13 weapon strike, applied on top of an excellent hit.
    /// </summary>
    public const double WeaponStrikeDamageFactor = 1.2;

    private const byte FirstWeaponGroup = 0;

    private const byte LastWeaponGroup = 5;

    private const byte FirstArmorGroup = 7;

    private const byte LastArmorGroup = 11;

    /// <summary>
    /// Determines whether the equipped items contain a complete armor set of the same kind where every piece is at least +13.
    /// </summary>
    /// <param name="equippedItems">The equipped items.</param>
    /// <returns><c>true</c> if the set bonus applies.</returns>
    public static bool IsCompleteSet(IEnumerable<Item> equippedItems)
    {
        var pieces = new Item?[5];
        foreach (var item in equippedItems)
        {
            if (!IsActive(item) || item.Level < RequiredLevel)
            {
                continue;
            }

            var slot = item.ItemSlot;
            if (slot < InventoryConstants.HelmSlot || slot > InventoryConstants.BootsSlot)
            {
                continue;
            }

            var group = item.Definition!.Group;
            if (group != FirstArmorGroup + slot - InventoryConstants.HelmSlot)
            {
                continue;
            }

            pieces[slot - InventoryConstants.HelmSlot] = item;
        }

        if (pieces.Any(p => p is null))
        {
            return false;
        }

        var number = pieces[0]!.Definition!.Number;
        return pieces.All(p => p!.Definition!.Number == number);
    }

    /// <summary>
    /// Determines whether a weapon of at least +13 is equipped.
    /// </summary>
    /// <param name="equippedItems">The equipped items.</param>
    /// <returns><c>true</c> if the weapon strike bonus applies.</returns>
    public static bool HasWeapon(IEnumerable<Item> equippedItems)
    {
        return equippedItems.Any(item => IsActive(item)
            && item.Level >= RequiredLevel
            && (item.ItemSlot == InventoryConstants.LeftHandSlot || item.ItemSlot == InventoryConstants.RightHandSlot)
            && item.Definition!.Group >= FirstWeaponGroup
            && item.Definition.Group <= LastWeaponGroup);
    }

    /// <summary>
    /// Creates the power-ups of the +13 bonuses for the equipped items.
    /// </summary>
    /// <param name="equippedItems">The equipped items.</param>
    /// <param name="attributeHolder">The attribute holder.</param>
    /// <returns>The power-ups.</returns>
    public static IEnumerable<PowerUpWrapper> GetPowerUps(IEnumerable<Item> equippedItems, AttributeSystem attributeHolder)
    {
        var items = equippedItems.ToList();
        if (IsCompleteSet(items))
        {
            yield return new PowerUpWrapper(new SimpleElement(SetVitality, AggregateType.AddRaw), Stats.TotalVitality, attributeHolder);
            yield return new PowerUpWrapper(new SimpleElement(SetDefenseIgnoreChance, AggregateType.AddRaw), Stats.DefenseIgnoreChance, attributeHolder);
        }

        if (HasWeapon(items))
        {
            yield return new PowerUpWrapper(new SimpleElement(WeaponStrikeChance, AggregateType.AddRaw), Stats.PlusThirteenStrikeChance, attributeHolder);
        }
    }

    private static bool IsActive(Item item)
    {
        return item.Definition is not null && item.Durability > 0 && !item.IsRentalExpired();
    }
}
