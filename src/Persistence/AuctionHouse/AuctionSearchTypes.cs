// <copyright file="AuctionSearchTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The order of browse results. Values match the wire catalog.
/// </summary>
public enum AuctionSearchSort
{
    /// <summary>
    /// Ascending by effective price (current bid, else starting price). Not valid for fruit baskets.
    /// </summary>
    PriceAscending = 0,

    /// <summary>
    /// Descending by effective price. Not valid for fruit baskets.
    /// </summary>
    PriceDescending = 1,

    /// <summary>
    /// The earliest expiry first.
    /// </summary>
    EndingSoonest = 2,

    /// <summary>
    /// The most recent listing first.
    /// </summary>
    Newest = 3,
}

/// <summary>
/// A browse request. Results are always restricted to one currency, because prices never compare across currencies.
/// </summary>
/// <param name="Currency">The currency mode as its wire value.</param>
/// <param name="Category">The category filter, or <c>null</c> for all categories.</param>
/// <param name="Sort">The order of results.</param>
/// <param name="Page">The page, starting at 1.</param>
/// <param name="PageSize">The number of listings per page.</param>
public readonly record struct AuctionListingSearch(
    byte Currency,
    byte? Category = null,
    AuctionSearchSort Sort = AuctionSearchSort.EndingSoonest,
    int Page = 1,
    int PageSize = 8);

/// <summary>
/// The browse-row summary of a listing.
/// </summary>
public readonly record struct AuctionListingRow(
    Guid ListingId,
    long ListingNumber,
    byte Currency,
    byte Category,
    long StartingPrice,
    long? CurrentBid,
    long? BuyoutPrice,
    DateTimeOffset EndsAt,
    int BidCount,
    Guid SellerAccountId,
    string SellerName,
    int ItemGroup,
    int ItemNumber,
    int ItemLevel,
    Guid ItemId,
    int Version,
    byte Status,
    long? StartingStrength,
    long? StartingAgility,
    long? StartingVitality,
    long? StartingEnergy,
    long? StartingCommand,
    long? CurrentBidStrength,
    long? CurrentBidAgility,
    long? CurrentBidVitality,
    long? CurrentBidEnergy,
    long? CurrentBidCommand,
    long? BuyoutStrength,
    long? BuyoutAgility,
    long? BuyoutVitality,
    long? BuyoutEnergy,
    long? BuyoutCommand)
{
    /// <summary>The starting basket of a fruit listing.</summary>
    public FruitBasket? StartingFruits => Basket(StartingStrength, StartingAgility, StartingVitality, StartingEnergy, StartingCommand);

    /// <summary>The current bid basket of a fruit listing, or <c>null</c> without a bid.</summary>
    public FruitBasket? CurrentFruits => Basket(CurrentBidStrength, CurrentBidAgility, CurrentBidVitality, CurrentBidEnergy, CurrentBidCommand);

    /// <summary>The buyout basket of a fruit listing, or <c>null</c> when there is none.</summary>
    public FruitBasket? BuyoutFruits => Basket(BuyoutStrength, BuyoutAgility, BuyoutVitality, BuyoutEnergy, BuyoutCommand);

    private static FruitBasket? Basket(long? strength, long? agility, long? vitality, long? energy, long? command)
        => strength is null ? null : new FruitBasket(strength ?? 0, agility ?? 0, vitality ?? 0, energy ?? 0, command ?? 0);
}

/// <summary>
/// One page of browse results, with the total number of matches.
/// </summary>
/// <param name="Rows">The listings on this page.</param>
/// <param name="TotalCount">The number of matching listings across all pages.</param>
public readonly record struct AuctionListingPage(IReadOnlyList<AuctionListingRow> Rows, int TotalCount);
