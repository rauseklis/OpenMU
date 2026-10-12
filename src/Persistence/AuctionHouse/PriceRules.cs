// <copyright file="PriceRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The pure rules for mixed prices. A price is a list of components of distinct kinds. A listing is either a
/// buy-now listing (no starting price; the buyout price, of one to five components, is the asking price) or an
/// auction (a single-component starting price, an optional buyout of the same kind, and bidding). Every comparison is
/// within one kind: no rule invents an exchange rate between Zen, jewels and fruits.
/// </summary>
public static class PriceRules
{
    /// <summary>
    /// Checks a listing's starting and buyout prices.
    /// </summary>
    /// <param name="starting">The starting price components; empty for a buy-now listing.</param>
    /// <param name="buyout">The buyout price components; empty when an auction has no buyout.</param>
    /// <param name="limits">The limits to apply; the approved defaults when <c>null</c>.</param>
    /// <returns>The result of the check.</returns>
    public static PriceValidationResult ValidateListing(
        IReadOnlyCollection<PriceComponent> starting,
        IReadOnlyCollection<PriceComponent> buyout,
        PriceLimits? limits = null)
    {
        limits ??= PriceLimits.Default;
        if (starting.Count == 0 && buyout.Count == 0)
        {
            return PriceValidationResult.NoPrice;
        }

        if (starting.Count == 0)
        {
            return ValidateComponents(buyout, limits);
        }

        if (starting.Count > 1)
        {
            return PriceValidationResult.MixedStartingPrice;
        }

        var startResult = ValidateComponents(starting, limits);
        if (startResult != PriceValidationResult.Valid || buyout.Count == 0)
        {
            return startResult;
        }

        var buyoutResult = ValidateComponents(buyout, limits);
        if (buyoutResult != PriceValidationResult.Valid)
        {
            return buyoutResult;
        }

        var start = starting.First();
        var buyoutComponent = buyout.First();
        if (buyout.Count != 1 || buyoutComponent.Kind != start.Kind)
        {
            return PriceValidationResult.BuyoutKindsDiffer;
        }

        return buyoutComponent.Quantity >= start.Quantity ? PriceValidationResult.Valid : PriceValidationResult.BuyoutBelowStart;
    }

    /// <summary>
    /// Determines whether a listing with this starting price takes bids. Only a single-component starting price does:
    /// bids on a mix could not be ranked against each other.
    /// </summary>
    /// <param name="starting">The starting price components.</param>
    /// <returns><c>true</c> if bids are allowed.</returns>
    public static bool IsBiddable(IReadOnlyCollection<PriceComponent> starting) => starting.Count == 1;

    /// <summary>
    /// Determines whether a kind is one of the seven jewels.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><c>true</c> for a jewel.</returns>
    public static bool IsJewel(PriceKind kind) => kind is >= PriceKind.Chaos and <= PriceKind.Harmony;

    /// <summary>
    /// Determines whether a kind is one of the five stat fruits.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><c>true</c> for a fruit.</returns>
    public static bool IsFruit(PriceKind kind) => kind is >= PriceKind.StrengthFruit and <= PriceKind.CommandFruit;

    /// <summary>
    /// Converts a scalar price in one currency into components: one component, or none for a zero price.
    /// </summary>
    /// <param name="currency">The scalar currency; <see cref="AuctionCurrencyMode.Fruits"/> is not scalar.</param>
    /// <param name="quantity">The quantity.</param>
    /// <returns>The components.</returns>
    public static IReadOnlyList<PriceComponent> FromScalar(AuctionCurrencyMode currency, long quantity)
    {
        if (currency is < AuctionCurrencyMode.Zen or > AuctionCurrencyMode.Harmony)
        {
            throw new ArgumentException($"{currency} is not a scalar currency.", nameof(currency));
        }

        return quantity > 0 ? new[] { new PriceComponent((PriceKind)(int)currency, quantity) } : Array.Empty<PriceComponent>();
    }

    /// <summary>
    /// Converts a fruit basket into components: one per nonzero fruit, in fruit order.
    /// </summary>
    /// <param name="basket">The basket.</param>
    /// <returns>The components.</returns>
    public static IReadOnlyList<PriceComponent> FromFruitBasket(FruitBasket basket)
    {
        var components = new List<PriceComponent>(5);
        AddIfPositive(components, PriceKind.StrengthFruit, basket.Strength);
        AddIfPositive(components, PriceKind.AgilityFruit, basket.Agility);
        AddIfPositive(components, PriceKind.VitalityFruit, basket.Vitality);
        AddIfPositive(components, PriceKind.EnergyFruit, basket.Energy);
        AddIfPositive(components, PriceKind.CommandFruit, basket.Command);
        return components;
    }

    /// <summary>
    /// Converts a price in the single-currency shape (a currency mode with a scalar or a fruit basket) into components.
    /// </summary>
    /// <param name="currency">The currency mode.</param>
    /// <param name="scalar">The scalar quantity, used unless the mode is Fruits.</param>
    /// <param name="basket">The fruit basket, used only when the mode is Fruits.</param>
    /// <returns>The components.</returns>
    public static IReadOnlyList<PriceComponent> FromLegacy(AuctionCurrencyMode currency, long scalar, FruitBasket basket)
    {
        return currency == AuctionCurrencyMode.Fruits ? FromFruitBasket(basket) : FromScalar(currency, scalar);
    }

    private static void AddIfPositive(List<PriceComponent> components, PriceKind kind, long quantity)
    {
        if (quantity > 0)
        {
            components.Add(new PriceComponent(kind, quantity));
        }
    }

    private static PriceValidationResult ValidateComponents(IReadOnlyCollection<PriceComponent> components, PriceLimits limits)
    {
        if (components.Count > limits.MaximumComponents)
        {
            return PriceValidationResult.TooManyComponents;
        }

        if (components.Select(component => component.Kind).Distinct().Count() != components.Count)
        {
            return PriceValidationResult.DuplicateComponent;
        }

        var inRange = components.All(component =>
            component.Quantity >= limits.MinimumOf(component.Kind) && component.Quantity <= limits.MaximumOf(component.Kind));
        return inRange ? PriceValidationResult.Valid : PriceValidationResult.QuantityOutOfRange;
    }
}
