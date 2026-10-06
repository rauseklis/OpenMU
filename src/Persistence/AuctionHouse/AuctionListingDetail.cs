// <copyright file="AuctionListingDetail.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

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
