// <copyright file="AuctionAccountRestriction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// A restriction on an account's use of the Auction House. An account has at most one restriction.
/// </summary>
[AggregateRoot]
public class AuctionAccountRestriction
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the restricted account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the expiry of the restriction, in UTC. Null means the restriction does not expire.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the reason for the restriction.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the administrator who set the restriction.
    /// </summary>
    public string AdministratorName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the listing-limit override for this account, if any.
    /// </summary>
    public int? ListingLimitOverride { get; set; }

    /// <summary>
    /// Gets or sets the time the restriction was last changed, in UTC.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
