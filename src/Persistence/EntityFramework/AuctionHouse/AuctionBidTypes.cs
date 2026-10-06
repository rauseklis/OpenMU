// <copyright file="AuctionBidTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

/// <summary>
/// The outcome of a bid.
/// </summary>
public enum AuctionBidOutcome
{
    /// <summary>
    /// The bid was placed and escrowed.
    /// </summary>
    Placed,

    /// <summary>
    /// The operation was already processed; nothing was placed again.
    /// </summary>
    Replayed,

    /// <summary>
    /// The listing does not exist.
    /// </summary>
    ListingNotFound,

    /// <summary>
    /// The listing changed since the client last saw it.
    /// </summary>
    StaleListing,

    /// <summary>
    /// The listing is not active, or has already expired.
    /// </summary>
    NotActive,

    /// <summary>
    /// The bidder is the seller.
    /// </summary>
    SelfBid,

    /// <summary>
    /// The bid is below the minimum acceptable bid.
    /// </summary>
    BidTooLow,

    /// <summary>
    /// The bidder does not have enough Zen.
    /// </summary>
    InsufficientCurrency,

    /// <summary>
    /// The listing's currency cannot yet be debited (jewel tender is not implemented).
    /// </summary>
    UnsupportedCurrency,
}

/// <summary>
/// The result of a bid.
/// </summary>
/// <param name="Outcome">The outcome.</param>
public readonly record struct AuctionBidResult(AuctionBidOutcome Outcome);

/// <summary>
/// A scalar bid (Zen or jewel listing). The minimum bid and the new expiry are computed by the domain rules before this.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="BidderAccountId">The bidder's account.</param>
/// <param name="BidderCharacterId">The bidder's character.</param>
/// <param name="ListingId">The listing bid on.</param>
/// <param name="ExpectedVersion">The listing version the client last saw.</param>
/// <param name="BidAmount">The bid, in whole units.</param>
/// <param name="MinimumBid">The minimum acceptable bid, from the domain rules.</param>
/// <param name="BidderIp">The bidder's IP address.</param>
/// <param name="Now">The time of the bid, in UTC.</param>
/// <param name="NewEndsAt">The expiry after anti-sniping, in UTC.</param>
public readonly record struct ScalarBidDraft(
    Guid OperationId,
    Guid BidderAccountId,
    Guid BidderCharacterId,
    Guid ListingId,
    int ExpectedVersion,
    long BidAmount,
    long MinimumBid,
    string? BidderIp,
    DateTimeOffset Now,
    DateTimeOffset NewEndsAt);
