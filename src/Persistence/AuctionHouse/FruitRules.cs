// <copyright file="FruitRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The pure rules for Fruit Basket pricing. Every comparison is per component: no rule invents an exchange rate
/// between fruit types, and a basket's nonzero component set is fixed when the listing is created.
/// </summary>
public static class FruitRules
{
    private const long MaximumComponentQuantity = 10_000;
    private const long MinimumMeaningfulQuantity = 2;
    private const int BidIncrementPercent = 5;
    private static readonly FruitComponent[] AllComponents = Enum.GetValues<FruitComponent>();

    /// <summary>
    /// Gets the Season 6 item level of a fruit on the Fruits item.
    /// </summary>
    /// <param name="component">The fruit component.</param>
    /// <returns>The item level.</returns>
    public static int GetItemLevel(FruitComponent component) => component switch
    {
        FruitComponent.Energy => 0,
        FruitComponent.Vitality => 1,
        FruitComponent.Agility => 2,
        FruitComponent.Strength => 3,
        _ => 4,
    };

    /// <summary>
    /// Determines whether a starting basket is valid: every component is within range, and at least one component
    /// is two or more, so the minimum success fee cannot reduce all seller proceeds to zero.
    /// </summary>
    /// <param name="basket">The basket.</param>
    /// <returns><c>true</c> if the basket is valid.</returns>
    public static bool IsValidStartingBasket(FruitBasket basket)
    {
        return AllComponents.All(component => basket.Get(component) is >= 0 and <= MaximumComponentQuantity)
               && AllComponents.Any(component => basket.Get(component) >= MinimumMeaningfulQuantity);
    }

    /// <summary>
    /// Calculates the fee for each nonzero component independently, using the same rounding and minimum as scalar fees.
    /// </summary>
    /// <param name="basket">The basket the fee applies to.</param>
    /// <param name="basisPoints">The fee rate in basis points.</param>
    /// <returns>A basket of fees with one value per component.</returns>
    public static FruitBasket CalculateFees(FruitBasket basket, int basisPoints)
    {
        return new FruitBasket(
            AuctionRules.CalculateFee(basket.Strength, basisPoints),
            AuctionRules.CalculateFee(basket.Agility, basisPoints),
            AuctionRules.CalculateFee(basket.Vitality, basisPoints),
            AuctionRules.CalculateFee(basket.Energy, basisPoints),
            AuctionRules.CalculateFee(basket.Command, basisPoints));
    }

    /// <summary>
    /// Determines whether a buyout is valid: it uses exactly the starting basket's nonzero components, and is at least
    /// the starting quantity of each of them.
    /// </summary>
    /// <param name="starting">The listing's starting basket.</param>
    /// <param name="buyout">The buyout basket.</param>
    /// <returns><c>true</c> if the buyout is valid.</returns>
    public static bool IsValidBuyout(FruitBasket starting, FruitBasket buyout)
    {
        return HasSameComponents(starting, buyout)
               && AllComponents.All(component => buyout.Get(component) is >= 0 and <= MaximumComponentQuantity)
               && AllComponents.All(component => buyout.Get(component) >= starting.Get(component));
    }

    /// <summary>
    /// Determines whether a bid is allowed. The first bid must meet every starting component. Every later bid must raise
    /// each nonzero component to at least the current quantity plus the per-component increment. In both cases the bid
    /// must use exactly the starting basket's components, so no component is added or omitted.
    /// </summary>
    /// <param name="starting">The listing's starting basket.</param>
    /// <param name="current">The current highest bid, or <c>null</c> when there is none.</param>
    /// <param name="bid">The bid basket.</param>
    /// <returns><c>true</c> if the bid is allowed.</returns>
    public static bool IsBidAllowed(FruitBasket starting, FruitBasket? current, FruitBasket bid)
    {
        if (!HasSameComponents(starting, bid) || !AllComponents.All(component => bid.Get(component) is >= 0 and <= MaximumComponentQuantity))
        {
            return false;
        }

        if (current is null)
        {
            return AllComponents.All(component => bid.Get(component) >= starting.Get(component));
        }

        var currentBasket = current.Value;
        return AllComponents.All(component =>
        {
            var currentQuantity = currentBasket.Get(component);
            if (currentQuantity <= 0)
            {
                return bid.Get(component) == 0;
            }

            var increment = Math.Max(1, (currentQuantity * BidIncrementPercent + 99) / 100);
            return bid.Get(component) >= currentQuantity + increment;
        });
    }

    private static bool HasSameComponents(FruitBasket left, FruitBasket right)
    {
        return AllComponents.All(component => (left.Get(component) > 0) == (right.Get(component) > 0));
    }
}
