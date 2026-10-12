// <copyright file="PriceLimits.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The limits a price must respect: how many components it may have and the quantity range of each kind. The defaults
/// are the approved values; the owner can change them in the Auction House configuration.
/// </summary>
public sealed record PriceLimits
{
    /// <summary>
    /// Gets the limits with every value at its approved default.
    /// </summary>
    public static PriceLimits Default { get; } = new();

    /// <summary>
    /// Gets the most components one price may have.
    /// </summary>
    public int MaximumComponents { get; init; } = 5;

    /// <summary>
    /// Gets the smallest Zen quantity.
    /// </summary>
    public long ZenMinimum { get; init; } = 1_000;

    /// <summary>
    /// Gets the largest Zen quantity.
    /// </summary>
    public long ZenMaximum { get; init; } = 2_000_000_000;

    /// <summary>
    /// Gets the smallest quantity of one jewel kind.
    /// </summary>
    public long JewelMinimum { get; init; } = 1;

    /// <summary>
    /// Gets the largest quantity of one jewel kind.
    /// </summary>
    public long JewelMaximum { get; init; } = 10_000;

    /// <summary>
    /// Gets the smallest quantity of one stat fruit kind.
    /// </summary>
    public long FruitMinimum { get; init; } = 1;

    /// <summary>
    /// Gets the largest quantity of one stat fruit kind.
    /// </summary>
    public long FruitMaximum { get; init; } = 10_000;

    /// <summary>
    /// Gets the smallest quantity allowed for a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The minimum quantity.</returns>
    public long MinimumOf(PriceKind kind)
    {
        return kind == PriceKind.Zen ? this.ZenMinimum : PriceRules.IsJewel(kind) ? this.JewelMinimum : this.FruitMinimum;
    }

    /// <summary>
    /// Gets the largest quantity allowed for a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The maximum quantity.</returns>
    public long MaximumOf(PriceKind kind)
    {
        return kind == PriceKind.Zen ? this.ZenMaximum : PriceRules.IsJewel(kind) ? this.JewelMaximum : this.FruitMaximum;
    }
}
