// <copyright file="AuctionSearchTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

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
    int Version);

/// <summary>
/// One page of browse results, with the total number of matches.
/// </summary>
/// <param name="Rows">The listings on this page.</param>
/// <param name="TotalCount">The number of matching listings across all pages.</param>
public readonly record struct AuctionListingPage(IReadOnlyList<AuctionListingRow> Rows, int TotalCount);
