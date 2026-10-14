// <copyright file="AuctionMixedTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A bid on a mixed-currency listing: a basket of up to five kinds. It must keep every currency of the current bid at the same
/// amount or higher and raise at least one; the first bid must cover the starting price in every currency of that price.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="BidderAccountId">The bidder's account.</param>
/// <param name="BidderCharacterId">The bidder's character, whose inventory pays.</param>
/// <param name="ListingId">The listing.</param>
/// <param name="ExpectedVersion">The listing version the client last saw.</param>
/// <param name="Bid">The bid basket.</param>
/// <param name="BidderIp">The bidder's address.</param>
/// <param name="Now">The time of the bid, in UTC.</param>
/// <param name="NewEndsAt">The expiry after anti-sniping.</param>
/// <param name="Limits">The price limits to check the bid against, or <c>null</c> for the defaults.</param>
public readonly record struct MixedBidDraft(
    Guid OperationId,
    Guid BidderAccountId,
    Guid BidderCharacterId,
    Guid ListingId,
    int ExpectedVersion,
    PriceBasket Bid,
    string? BidderIp,
    DateTimeOffset Now,
    DateTimeOffset NewEndsAt,
    PriceLimits? Limits = null);

/// <summary>
/// A buyout of a mixed-currency listing at its buyout basket.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="BuyerAccountId">The buyer's account.</param>
/// <param name="BuyerCharacterId">The buyer's character, whose inventory pays.</param>
/// <param name="ListingId">The listing.</param>
/// <param name="ExpectedVersion">The listing version the client last saw.</param>
/// <param name="BuyerIp">The buyer's address.</param>
/// <param name="Now">The time of the buyout, in UTC.</param>
public readonly record struct MixedBuyoutDraft(
    Guid OperationId,
    Guid BuyerAccountId,
    Guid BuyerCharacterId,
    Guid ListingId,
    int ExpectedVersion,
    string? BuyerIp,
    DateTimeOffset Now);
