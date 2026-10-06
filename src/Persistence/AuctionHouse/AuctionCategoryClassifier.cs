// <copyright file="AuctionCategoryClassifier.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Assigns the browse category of an item. Exact material pairs are checked first, because item numbering differs between
/// game versions and a jewel can sit inside a group that would otherwise be a wing or a consumable.
/// </summary>
public static class AuctionCategoryClassifier
{
    private static readonly (int Group, int Number)[] DefaultMaterialPairs =
    {
        (12, 15),
        (14, 13),
        (14, 14),
        (14, 16),
        (14, 22),
        (14, 31),
        (14, 42),
    };

    /// <summary>
    /// Classifies an item with the default material pairs.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <returns>The category.</returns>
    public static AuctionCategory Classify(int group, int number) => Classify(group, number, DefaultMaterialPairs);

    /// <summary>
    /// Classifies an item with an explicit list of material pairs.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="materialPairs">The exact item pairs classified as jewel or material.</param>
    /// <returns>The category.</returns>
    public static AuctionCategory Classify(int group, int number, IEnumerable<(int Group, int Number)> materialPairs)
    {
        if (materialPairs.Any(pair => pair.Group == group && pair.Number == number))
        {
            return AuctionCategory.JewelMaterial;
        }

        return group switch
        {
            >= 0 and <= 5 => AuctionCategory.Weapon,
            >= 6 and <= 11 => AuctionCategory.Armor,
            12 => AuctionCategory.Wing,
            13 => AuctionCategory.PetHelper,
            14 => AuctionCategory.Consumable,
            _ => AuctionCategory.Miscellaneous,
        };
    }
}
