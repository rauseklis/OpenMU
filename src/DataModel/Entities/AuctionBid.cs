// <copyright file="AuctionBid.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// An immutable bid on an Auction House listing, with its escrow state. Currency and status are stored as wire values.
/// </summary>
[AggregateRoot]
public class AuctionBid
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the listing the bid belongs to.
    /// </summary>
    public Guid ListingId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the bidder's account.
    /// </summary>
    public Guid BidderAccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the bidder's character.
    /// </summary>
    public Guid BidderCharacterId { get; set; }

    /// <summary>
    /// Gets or sets the currency mode as its wire value. Must equal the listing's currency.
    /// </summary>
    public byte Currency { get; set; }

    /// <summary>
    /// Gets or sets the scalar amount, used by Zen and jewel modes.
    /// </summary>
    public long? ScalarAmount { get; set; }

    /// <summary>
    /// Gets or sets the Strength component, used by the Fruits mode.
    /// </summary>
    public long? StrengthAmount { get; set; }

    /// <summary>
    /// Gets or sets the Agility component, used by the Fruits mode.
    /// </summary>
    public long? AgilityAmount { get; set; }

    /// <summary>
    /// Gets or sets the Vitality component, used by the Fruits mode.
    /// </summary>
    public long? VitalityAmount { get; set; }

    /// <summary>
    /// Gets or sets the Energy component, used by the Fruits mode.
    /// </summary>
    public long? EnergyAmount { get; set; }

    /// <summary>
    /// Gets or sets the Command component, used by the Fruits mode.
    /// </summary>
    public long? CommandAmount { get; set; }

    /// <summary>
    /// Gets or sets the bidder's IP address at the time of the bid.
    /// </summary>
    public string? BidderIp { get; set; }

    /// <summary>
    /// Gets or sets the escrow status as its wire value: 0 Escrowed, 1 RefundQueued, 2 Won, 3 Consumed.
    /// </summary>
    public byte Status { get; set; }

    /// <summary>
    /// Gets or sets the time the bid was placed, in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the time the bid's escrow was closed, in UTC.
    /// </summary>
    public DateTimeOffset? ClosedAt { get; set; }
}
