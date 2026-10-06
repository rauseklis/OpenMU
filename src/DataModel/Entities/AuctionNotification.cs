// <copyright file="AuctionNotification.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// A durable notification for an account. Online delivery is best effort; the Mailbox is the durable truth.
/// </summary>
[AggregateRoot]
public class AuctionNotification
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account to notify.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the listing involved, if any.
    /// </summary>
    public Guid? ListingId { get; set; }

    /// <summary>
    /// Gets or sets the notification kind as its wire value.
    /// </summary>
    public byte Kind { get; set; }

    /// <summary>
    /// Gets or sets the currency mode as its wire value, if relevant.
    /// </summary>
    public byte? Currency { get; set; }

    /// <summary>
    /// Gets or sets the scalar amount, if relevant.
    /// </summary>
    public long? Amount { get; set; }

    /// <summary>
    /// Gets or sets the time the notification was created, in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the time the notification was delivered to a connected client, in UTC. Null while undelivered.
    /// </summary>
    public DateTimeOffset? DeliveredAt { get; set; }
}
