// <copyright file="AdminRemoveTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// An administrative removal of an active listing.
/// </summary>
/// <param name="ListingId">The listing to remove.</param>
/// <param name="AdministratorIdentity">The administrator who removes the listing, recorded in the audit row.</param>
/// <param name="Reason">The reason, recorded in the audit row.</param>
/// <param name="Now">The current time, in UTC.</param>
public readonly record struct AdminRemoveDraft(Guid ListingId, string AdministratorIdentity, string Reason, DateTimeOffset Now);

/// <summary>
/// The outcome of an administrative removal.
/// </summary>
public enum AdminRemoveOutcome
{
    /// <summary>The listing was removed; its item returned to the seller and any escrow refunded.</summary>
    Removed,

    /// <summary>No listing has the identifier.</summary>
    ListingNotFound,

    /// <summary>The listing is not active, so it cannot be removed.</summary>
    NotActive,
}
