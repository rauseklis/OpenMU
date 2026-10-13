// <copyright file="AuctionCategoryClassifier.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Assigns the browse category of an item. The category and sub-category tables live in <see cref="AuctionTaxonomy"/>.
/// </summary>
public static class AuctionCategoryClassifier
{
    /// <summary>
    /// Classifies an item.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <returns>The category.</returns>
    public static AuctionCategory Classify(int group, int number) => AuctionTaxonomy.Classify(group, number).Category;

    /// <summary>
    /// Classifies an item, treating an explicit list of exact item pairs as jewels or materials before the taxonomy is consulted.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="materialPairs">The exact item pairs classified as jewel or material.</param>
    /// <returns>The category.</returns>
    public static AuctionCategory Classify(int group, int number, IEnumerable<(int Group, int Number)> materialPairs)
        => materialPairs.Any(pair => pair.Group == group && pair.Number == number)
            ? AuctionCategory.JewelMaterial
            : AuctionTaxonomy.Classify(group, number).Category;
}
