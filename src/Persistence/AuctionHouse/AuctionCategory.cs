// <copyright file="AuctionCategory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The browse category of a listing. Values match the wire catalog.
/// </summary>
public enum AuctionCategory
{
    /// <summary>
    /// Weapons (groups 0 to 5).
    /// </summary>
    Weapon = 0,

    /// <summary>
    /// Armor (groups 6 to 11).
    /// </summary>
    Armor = 1,

    /// <summary>
    /// Wings (group 12, excluding configured materials).
    /// </summary>
    Wing = 2,

    /// <summary>
    /// Pets and helpers (group 13).
    /// </summary>
    PetHelper = 3,

    /// <summary>
    /// Jewels and crafting materials, identified by exact item pairs.
    /// </summary>
    JewelMaterial = 4,

    /// <summary>
    /// Consumables (group 14, excluding configured materials).
    /// </summary>
    Consumable = 5,

    /// <summary>
    /// Everything else.
    /// </summary>
    Miscellaneous = 6,

    /// <summary>
    /// Skill orbs, crystals, scrolls and parchments (group 15, and the orb and scroll entries of group 12).
    /// </summary>
    ScrollOrb = 7,
}
