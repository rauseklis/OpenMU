// <copyright file="ListingStateMachine.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The lifecycle of a listing.
/// </summary>
public enum ListingStatus
{
    /// <summary>
    /// The listing is open for bids and buyouts.
    /// </summary>
    Active,

    /// <summary>
    /// The listing was sold.
    /// </summary>
    Sold,

    /// <summary>
    /// The listing ended without a bid.
    /// </summary>
    Expired,

    /// <summary>
    /// The seller cancelled the listing.
    /// </summary>
    Cancelled,

    /// <summary>
    /// An administrator removed the listing.
    /// </summary>
    AdminRemoved,
}

/// <summary>
/// The lifecycle of a collection entry in the Mailbox.
/// </summary>
public enum CollectionStatus
{
    /// <summary>
    /// Nothing has been claimed yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Part of the entry has been claimed.
    /// </summary>
    PartiallyClaimed,

    /// <summary>
    /// The entry is fully claimed. This is final.
    /// </summary>
    Claimed,
}

/// <summary>
/// The approved listing and collection transitions. Anything not listed here is rejected.
/// </summary>
public static class ListingStateMachine
{
    /// <summary>
    /// Determines whether a listing may move from one status to another.
    /// </summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The requested status.</param>
    /// <returns><c>true</c> if the transition is allowed.</returns>
    public static bool CanTransition(ListingStatus from, ListingStatus to)
    {
        return from == ListingStatus.Active
               && (to == ListingStatus.Sold || to == ListingStatus.Expired || to == ListingStatus.Cancelled || to == ListingStatus.AdminRemoved);
    }

    /// <summary>
    /// Determines whether the seller may cancel a listing. Only an active listing with no bids can be cancelled.
    /// </summary>
    /// <param name="status">The listing status.</param>
    /// <param name="bidCount">The number of bids placed.</param>
    /// <returns><c>true</c> if cancellation is allowed.</returns>
    public static bool CanCancel(ListingStatus status, int bidCount) => status == ListingStatus.Active && bidCount == 0;

    /// <summary>
    /// Determines whether a collection entry may move from one status to another.
    /// </summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The requested status.</param>
    /// <returns><c>true</c> if the transition is allowed.</returns>
    public static bool CanTransitionCollection(CollectionStatus from, CollectionStatus to)
    {
        return from switch
        {
            CollectionStatus.Pending => to == CollectionStatus.PartiallyClaimed || to == CollectionStatus.Claimed,
            CollectionStatus.PartiallyClaimed => to == CollectionStatus.PartiallyClaimed || to == CollectionStatus.Claimed,
            _ => false,
        };
    }
}
