// <copyright file="AuctionServiceTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

/// <summary>
/// The outcome of a service request. Values match the wire result catalog, so the packet handler converts them directly.
/// </summary>
public enum AuctionResultCode
{
    /// <summary>The request succeeded.</summary>
    Success = 0,

    /// <summary>The feature is disabled.</summary>
    Disabled = 1,

    /// <summary>The request is malformed or out of range.</summary>
    InvalidRequest = 8,

    /// <summary>The listing does not exist.</summary>
    ListingNotFound = 18,
}

/// <summary>
/// The result of opening the window.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="PendingMailbox">The number of collections the account has not finished claiming.</param>
/// <param name="ServerTime">The server time, in UTC.</param>
/// <param name="PageSize">The configured number of listings per page.</param>
public readonly record struct AuctionOpenResult(AuctionResultCode Result, int PendingMailbox, DateTimeOffset ServerTime, int PageSize);

/// <summary>
/// The result of a browse request.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="Page">The listings on the page; empty when the request failed.</param>
/// <param name="ServerTime">The server time, in UTC.</param>
public readonly record struct AuctionBrowseResult(AuctionResultCode Result, AuctionListingPage Page, DateTimeOffset ServerTime);

/// <summary>
/// The result of a detail request.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="Listing">The listing; <c>null</c> when the request failed.</param>
/// <param name="ServerTime">The server time, in UTC.</param>
public readonly record struct AuctionDetailResult(AuctionResultCode Result, AuctionListingDetail? Listing, DateTimeOffset ServerTime);

/// <summary>
/// The detail view of one listing: the fields a player may see, without the seller's network address or bidder identities.
/// </summary>
/// <param name="ListingId">The listing.</param>
/// <param name="Version">The listing version, for optimistic mutations.</param>
/// <param name="Status">The listing status.</param>
/// <param name="Currency">The currency mode.</param>
/// <param name="Category">The category.</param>
/// <param name="ItemId">The auctioned item.</param>
/// <param name="StartingPrice">The starting price.</param>
/// <param name="BuyoutPrice">The buyout price, or <c>null</c> when there is none.</param>
/// <param name="CurrentBid">The current bid, or <c>null</c> when there is none.</param>
/// <param name="EndsAt">When the listing ends, in UTC.</param>
/// <param name="BidCount">The number of bids.</param>
/// <param name="SellerName">The seller's name.</param>
/// <param name="Note">The seller's note.</param>
public sealed record AuctionListingDetail(
    Guid ListingId,
    int Version,
    byte Status,
    byte Currency,
    byte Category,
    Guid ItemId,
    long StartingPrice,
    long? BuyoutPrice,
    long? CurrentBid,
    DateTimeOffset EndsAt,
    int BidCount,
    string SellerName,
    string? Note);
