// <copyright file="AuctionCancelExpireTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

/// <summary>
/// The outcome of a cancellation.
/// </summary>
public enum AuctionCancelOutcome
{
    /// <summary>
    /// The listing was cancelled and its item returned to the seller.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The operation was already processed; nothing was cancelled again.
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
    /// The listing is not active.
    /// </summary>
    NotActive,

    /// <summary>
    /// The requester is not the seller.
    /// </summary>
    NotSeller,

    /// <summary>
    /// The listing has bids and cannot be cancelled.
    /// </summary>
    CannotCancelWithBid,
}

/// <summary>
/// The result of a cancellation.
/// </summary>
/// <param name="Outcome">The outcome.</param>
public readonly record struct AuctionCancelResult(AuctionCancelOutcome Outcome);

/// <summary>
/// A cancellation request.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="AccountId">The requester's account.</param>
/// <param name="ListingId">The listing to cancel.</param>
/// <param name="ExpectedVersion">The listing version the client last saw.</param>
/// <param name="Now">The time of the request, in UTC.</param>
public readonly record struct CancelDraft(Guid OperationId, Guid AccountId, Guid ListingId, int ExpectedVersion, DateTimeOffset Now);

/// <summary>
/// The outcome of an expiry check.
/// </summary>
public enum AuctionExpireOutcome
{
    /// <summary>
    /// The listing was finalized: sold to its high bidder, or returned to the seller.
    /// </summary>
    Finalized,

    /// <summary>
    /// The listing is not due, is not active, or was already finalized.
    /// </summary>
    NotDue,

    /// <summary>
    /// The listing does not exist.
    /// </summary>
    ListingNotFound,
}

/// <summary>
/// The result of an expiry check.
/// </summary>
/// <param name="Outcome">The outcome.</param>
public readonly record struct AuctionExpireResult(AuctionExpireOutcome Outcome);

/// <summary>
/// An expiry check. The success fee is computed by the domain rules before this.
/// </summary>
/// <param name="ListingId">The listing to check.</param>
/// <param name="SuccessFee">The success fee charged against a sale, from the domain rules.</param>
/// <param name="Now">The current time, in UTC.</param>
public readonly record struct ExpireDraft(Guid ListingId, long SuccessFee, DateTimeOffset Now);
