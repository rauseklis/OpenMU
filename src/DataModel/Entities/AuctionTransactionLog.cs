// <copyright file="AuctionTransactionLog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// An append-only audit record of an Auction House action. The application never updates or deletes these rows.
/// </summary>
[AggregateRoot]
public class AuctionTransactionLog
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the action as its wire value.
    /// </summary>
    public byte Action { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the listing involved, if any.
    /// </summary>
    public Guid? ListingId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the acting account.
    /// </summary>
    public Guid ActorAccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the acting character.
    /// </summary>
    public Guid? ActorCharacterId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the counterpart account, if any.
    /// </summary>
    public Guid? CounterpartAccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the item involved, if any.
    /// </summary>
    public Guid? ItemId { get; set; }

    /// <summary>
    /// Gets or sets the currency mode as its wire value, if any.
    /// </summary>
    public byte? Currency { get; set; }

    /// <summary>
    /// Gets or sets the scalar amount involved, if any.
    /// </summary>
    public long? Amount { get; set; }

    /// <summary>
    /// Gets or sets the fee charged, if any.
    /// </summary>
    public long? Fee { get; set; }

    /// <summary>
    /// Gets or sets the balance before the action, if relevant.
    /// </summary>
    public long? BalanceBefore { get; set; }

    /// <summary>
    /// Gets or sets the balance after the action, if relevant.
    /// </summary>
    public long? BalanceAfter { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the operation which caused the action, if any.
    /// </summary>
    public Guid? OperationId { get; set; }

    /// <summary>
    /// Gets or sets the acting client's IP address.
    /// </summary>
    public string? ActorIp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the action was flagged as suspicious. A flag never rejects the action.
    /// </summary>
    public bool IsSuspicious { get; set; }

    /// <summary>
    /// Gets or sets the reason for the flag or the administrative action, if any.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the time of the action, in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
