// <copyright file="AuctionOperation.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// The idempotency record of an economic operation. The pair of account and client operation identifier is unique, so a
/// replayed request returns its stored outcome without moving anything again.
/// </summary>
[AggregateRoot]
public class AuctionOperation
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account which issued the operation.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the client-generated operation identifier.
    /// </summary>
    public Guid OperationId { get; set; }

    /// <summary>
    /// Gets or sets the operation kind as its wire value.
    /// </summary>
    public byte Kind { get; set; }

    /// <summary>
    /// Gets or sets the outcome as its wire value.
    /// </summary>
    public int Outcome { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the related listing, if any.
    /// </summary>
    public Guid? ListingId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the related collection, if any.
    /// </summary>
    public Guid? CollectionId { get; set; }

    /// <summary>
    /// Gets or sets the time the operation was recorded, in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
