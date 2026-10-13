// <copyright file="AuctionTaxonomy.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The browse category and sub-category of an item.
/// </summary>
/// <param name="Category">The category.</param>
/// <param name="SubCategory">The sub-category within the category; the constants in <see cref="AuctionTaxonomy"/> name the values.</param>
public readonly record struct AuctionClassification(AuctionCategory Category, byte SubCategory);

/// <summary>
/// Maps an item (group and number) to its browse category and sub-category. Exact numbers and ranges are checked first,
/// in the order they are listed, then the group's default. A number no rule names falls to its group's default, so a stock
/// item set still classifies sensibly. Sub-category values are per category.
/// </summary>
public static class AuctionTaxonomy
{
    /// <summary>
    /// The wire value which means every category.
    /// </summary>
    public const byte AllCategories = byte.MaxValue;

    /// <summary>
    /// The wire value which means every sub-category of the category. A listing stores it until it has been classified.
    /// </summary>
    public const byte AllSubCategories = byte.MaxValue;

    private static readonly Rule[] Rules = BuildRules();

    /// <summary>
    /// Sub-categories of <see cref="AuctionCategory.Weapon"/>; they equal the item group.
    /// </summary>
    public static class WeaponKinds
    {
        /// <summary>Swords, group 0.</summary>
        public const byte Swords = 0;

        /// <summary>Axes, group 1.</summary>
        public const byte Axes = 1;

        /// <summary>Maces and scepters, group 2.</summary>
        public const byte MacesAndScepters = 2;

        /// <summary>Spears, group 3.</summary>
        public const byte Spears = 3;

        /// <summary>Bows, crossbows and their ammunition, group 4.</summary>
        public const byte BowsAndCrossbows = 4;

        /// <summary>Staves and books, group 5.</summary>
        public const byte StavesAndBooks = 5;
    }

    /// <summary>
    /// Sub-categories of <see cref="AuctionCategory.Armor"/>.
    /// </summary>
    public static class ArmorKinds
    {
        /// <summary>Helms, group 7.</summary>
        public const byte Helms = 0;

        /// <summary>Armors, group 8.</summary>
        public const byte Armors = 1;

        /// <summary>Pants, group 9.</summary>
        public const byte Pants = 2;

        /// <summary>Gloves, group 10.</summary>
        public const byte Gloves = 3;

        /// <summary>Boots, group 11.</summary>
        public const byte Boots = 4;

        /// <summary>Shields, group 6.</summary>
        public const byte Shields = 5;
    }

    /// <summary>
    /// Sub-categories of <see cref="AuctionCategory.Wing"/>.
    /// </summary>
    public static class WingKinds
    {
        /// <summary>Wings.</summary>
        public const byte Wings = 0;

        /// <summary>Capes and cloaks.</summary>
        public const byte CapesAndCloaks = 1;
    }

    /// <summary>
    /// Sub-categories of <see cref="AuctionCategory.PetHelper"/>.
    /// </summary>
    public static class PetKinds
    {
        /// <summary>Pets and helpers.</summary>
        public const byte Pets = 0;

        /// <summary>Mounts.</summary>
        public const byte Mounts = 1;

        /// <summary>Rings.</summary>
        public const byte Rings = 2;

        /// <summary>Pendants and necklaces.</summary>
        public const byte Pendants = 3;

        /// <summary>Eggs.</summary>
        public const byte Eggs = 4;

        /// <summary>Seals, tickets and everything else of group 13.</summary>
        public const byte SealsAndTickets = 5;
    }

    /// <summary>
    /// Sub-categories of <see cref="AuctionCategory.JewelMaterial"/>.
    /// </summary>
    public static class MaterialKinds
    {
        /// <summary>Jewels.</summary>
        public const byte Jewels = 0;

        /// <summary>Bundled jewels and stones.</summary>
        public const byte Bundles = 1;

        /// <summary>Refining stones and gemstones.</summary>
        public const byte RefiningStones = 2;

        /// <summary>Seeds, spheres and seed spheres.</summary>
        public const byte SeedsAndSpheres = 3;

        /// <summary>Stat fruits and reset fruits.</summary>
        public const byte Fruits = 4;
    }

    /// <summary>
    /// Sub-categories of <see cref="AuctionCategory.Consumable"/>.
    /// </summary>
    public static class ConsumableKinds
    {
        /// <summary>Potions.</summary>
        public const byte Potions = 0;

        /// <summary>Elixirs and buff scrolls.</summary>
        public const byte ElixirsAndBuffs = 1;

        /// <summary>Boxes, tickets and everything else of group 14.</summary>
        public const byte BoxesAndMisc = 2;
    }

    /// <summary>
    /// Sub-categories of <see cref="AuctionCategory.ScrollOrb"/>.
    /// </summary>
    public static class ScrollKinds
    {
        /// <summary>Skill orbs and crystals.</summary>
        public const byte OrbsAndCrystals = 0;

        /// <summary>Skill scrolls and parchments.</summary>
        public const byte ScrollsAndParchments = 1;
    }

    /// <summary>
    /// Classifies an item.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <returns>The category and sub-category.</returns>
    public static AuctionClassification Classify(int group, int number)
    {
        foreach (var rule in Rules)
        {
            if (rule.Group == group && number >= rule.First && number <= rule.Last)
            {
                return new AuctionClassification(rule.Category, rule.SubCategory);
            }
        }

        return new AuctionClassification(AuctionCategory.Miscellaneous, 0);
    }

    /// <summary>
    /// Gets the sub-category filter of a browse request. A sub-category only filters when a specific category and a specific
    /// sub-category are both named; otherwise it is ignored.
    /// </summary>
    /// <param name="category">The requested category, <see cref="AllCategories"/> for every category.</param>
    /// <param name="subCategory">The requested sub-category, <see cref="AllSubCategories"/> for every sub-category.</param>
    /// <returns>The sub-category to filter on, or <c>null</c> for no sub-category filter.</returns>
    public static byte? ToSubCategoryFilter(byte category, byte subCategory)
        => category == AllCategories || subCategory == AllSubCategories ? null : subCategory;

    private static Rule[] BuildRules()
    {
        var rules = new List<Rule>();

        void Range(int group, int first, int last, AuctionCategory category, byte subCategory)
            => rules.Add(new Rule(group, first, last, category, subCategory));

        void Numbers(int group, AuctionCategory category, byte subCategory, params int[] numbers)
        {
            foreach (var number in numbers)
            {
                Range(group, number, number, category, subCategory);
            }
        }

        // Jewels and materials.
        Numbers(12, AuctionCategory.JewelMaterial, MaterialKinds.Jewels, 15);
        Numbers(14, AuctionCategory.JewelMaterial, MaterialKinds.Jewels, 13, 14, 16, 22, 31, 42, 160, 161);
        Range(14, 244, 259, AuctionCategory.JewelMaterial, MaterialKinds.Jewels);
        Range(12, 30, 31, AuctionCategory.JewelMaterial, MaterialKinds.Bundles);
        Range(12, 136, 143, AuctionCategory.JewelMaterial, MaterialKinds.Bundles);
        Numbers(14, AuctionCategory.JewelMaterial, MaterialKinds.RefiningStones, 41, 43, 44);
        Range(12, 60, 65, AuctionCategory.JewelMaterial, MaterialKinds.SeedsAndSpheres);
        Range(12, 70, 74, AuctionCategory.JewelMaterial, MaterialKinds.SeedsAndSpheres);
        Range(12, 100, 129, AuctionCategory.JewelMaterial, MaterialKinds.SeedsAndSpheres);
        Numbers(13, AuctionCategory.JewelMaterial, MaterialKinds.Fruits, 15);
        Range(13, 54, 58, AuctionCategory.JewelMaterial, MaterialKinds.Fruits);

        // Scrolls and orbs.
        Range(12, 7, 14, AuctionCategory.ScrollOrb, ScrollKinds.OrbsAndCrystals);
        Range(12, 16, 19, AuctionCategory.ScrollOrb, ScrollKinds.OrbsAndCrystals);
        Range(12, 44, 47, AuctionCategory.ScrollOrb, ScrollKinds.OrbsAndCrystals);
        Numbers(12, AuctionCategory.ScrollOrb, ScrollKinds.ScrollsAndParchments, 21, 22, 23, 24, 35, 48);

        // Capes and cloaks, including the Cape of Lord which lives in group 13.
        Numbers(12, AuctionCategory.Wing, WingKinds.CapesAndCloaks, 40, 49, 50, 130, 135, 154, 156, 161, 163, 208, 210, 211, 234, 240, 242);
        Numbers(13, AuctionCategory.Wing, WingKinds.CapesAndCloaks, 30);

        // Group 12 number 26 is a gem which belongs nowhere else.
        Numbers(12, AuctionCategory.Miscellaneous, 0, 26);

        // Pets, mounts, rings, pendants and eggs of group 13.
        Numbers(13, AuctionCategory.PetHelper, PetKinds.Pets, 0, 1, 4, 5, 64, 65, 67, 80, 106, 123, 145);
        Numbers(13, AuctionCategory.PetHelper, PetKinds.Mounts, 2, 3, 37);
        Range(13, 400, 424, AuctionCategory.PetHelper, PetKinds.Mounts);
        Numbers(13, AuctionCategory.PetHelper, PetKinds.Rings, 8, 9, 10, 20, 21, 22, 23, 24, 39, 40, 41, 42, 68, 76, 107, 109, 110, 111, 112, 122);
        Numbers(13, AuctionCategory.PetHelper, PetKinds.Pendants, 12, 13, 25, 26, 27, 28, 38, 113, 114, 115);
        Range(13, 200, 276, AuctionCategory.PetHelper, PetKinds.Eggs);

        // Potions, elixirs and buff scrolls of group 14.
        Range(14, 0, 8, AuctionCategory.Consumable, ConsumableKinds.Potions);
        Range(14, 35, 40, AuctionCategory.Consumable, ConsumableKinds.Potions);
        Numbers(14, AuctionCategory.Consumable, ConsumableKinds.Potions, 70, 71, 94, 133);
        Range(14, 72, 82, AuctionCategory.Consumable, ConsumableKinds.ElixirsAndBuffs);
        Numbers(14, AuctionCategory.Consumable, ConsumableKinds.ElixirsAndBuffs, 97, 98, 140);

        // Group defaults, always last.
        Range(0, 0, int.MaxValue, AuctionCategory.Weapon, WeaponKinds.Swords);
        Range(1, 0, int.MaxValue, AuctionCategory.Weapon, WeaponKinds.Axes);
        Range(2, 0, int.MaxValue, AuctionCategory.Weapon, WeaponKinds.MacesAndScepters);
        Range(3, 0, int.MaxValue, AuctionCategory.Weapon, WeaponKinds.Spears);
        Range(4, 0, int.MaxValue, AuctionCategory.Weapon, WeaponKinds.BowsAndCrossbows);
        Range(5, 0, int.MaxValue, AuctionCategory.Weapon, WeaponKinds.StavesAndBooks);
        Range(6, 0, int.MaxValue, AuctionCategory.Armor, ArmorKinds.Shields);
        Range(7, 0, int.MaxValue, AuctionCategory.Armor, ArmorKinds.Helms);
        Range(8, 0, int.MaxValue, AuctionCategory.Armor, ArmorKinds.Armors);
        Range(9, 0, int.MaxValue, AuctionCategory.Armor, ArmorKinds.Pants);
        Range(10, 0, int.MaxValue, AuctionCategory.Armor, ArmorKinds.Gloves);
        Range(11, 0, int.MaxValue, AuctionCategory.Armor, ArmorKinds.Boots);
        Range(12, 0, int.MaxValue, AuctionCategory.Wing, WingKinds.Wings);
        Range(13, 0, int.MaxValue, AuctionCategory.PetHelper, PetKinds.SealsAndTickets);
        Range(14, 0, int.MaxValue, AuctionCategory.Consumable, ConsumableKinds.BoxesAndMisc);
        Range(15, 0, int.MaxValue, AuctionCategory.ScrollOrb, ScrollKinds.ScrollsAndParchments);

        return rules.ToArray();
    }

    private readonly record struct Rule(int Group, int First, int Last, AuctionCategory Category, byte SubCategory);
}
