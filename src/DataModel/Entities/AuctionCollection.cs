// <copyright file="AuctionCollection.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// A durable, account-wide delivery obligation: a purchased or returned item, sale proceeds, a refund or change.
/// Exactly one of the item, the scalar obligation or the fruit-basket obligation is set.
/// </summary>
[AggregateRoot]
public class AuctionCollection
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the 64-bit collection number used on the wire. Assigned by the database.
    /// </summary>
    public long CollectionNumber { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account which may claim the collection.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the listing this collection came from, if any.
    /// </summary>
    public Guid? ListingId { get; set; }

    /// <summary>
    /// Gets or sets the collection kind as its wire value.
    /// </summary>
    public byte Kind { get; set; }

    /// <summary>
    /// Gets or sets the currency mode as its wire value.
    /// </summary>
    public byte Currency { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the lot item, for item collections. A pending lot item appears in only one collection.
    /// </summary>
    public Guid? ItemId { get; set; }

    /// <summary>
    /// Gets or sets the original scalar amount, for scalar obligations.
    /// </summary>
    public long? OriginalScalar { get; set; }

    /// <summary>
    /// Gets or sets the remaining scalar amount, for scalar obligations.
    /// </summary>
    public long? RemainingScalar { get; set; }

    /// <summary>
    /// Gets or sets the original Strength component, for fruit-basket obligations.
    /// </summary>
    public long? OriginalStrength { get; set; }

    /// <summary>
    /// Gets or sets the original Agility component, for fruit-basket obligations.
    /// </summary>
    public long? OriginalAgility { get; set; }

    /// <summary>
    /// Gets or sets the original Vitality component, for fruit-basket obligations.
    /// </summary>
    public long? OriginalVitality { get; set; }

    /// <summary>
    /// Gets or sets the original Energy component, for fruit-basket obligations.
    /// </summary>
    public long? OriginalEnergy { get; set; }

    /// <summary>
    /// Gets or sets the original Command component, for fruit-basket obligations.
    /// </summary>
    public long? OriginalCommand { get; set; }

    /// <summary>
    /// Gets or sets the remaining Strength component, for fruit-basket obligations.
    /// </summary>
    public long? RemainingStrength { get; set; }

    /// <summary>
    /// Gets or sets the remaining Agility component, for fruit-basket obligations.
    /// </summary>
    public long? RemainingAgility { get; set; }

    /// <summary>
    /// Gets or sets the remaining Vitality component, for fruit-basket obligations.
    /// </summary>
    public long? RemainingVitality { get; set; }

    /// <summary>
    /// Gets or sets the remaining Energy component, for fruit-basket obligations.
    /// </summary>
    public long? RemainingEnergy { get; set; }

    /// <summary>
    /// Gets or sets the remaining Command component, for fruit-basket obligations.
    /// </summary>
    public long? RemainingCommand { get; set; }

    /// <summary>
    /// Gets or sets the original amount of a mixed-currency obligation as 13 comma-separated quantities in price-kind order.
    /// </summary>
    public string? OriginalVector { get; set; }

    /// <summary>
    /// Gets or sets the remaining amount of a mixed-currency obligation as 13 comma-separated quantities in price-kind order.
    /// A mixed obligation is claimed all at once, so it is either the original amount or all zeros.
    /// </summary>
    public string? RemainingVector { get; set; }

    /// <summary>
    /// Gets or sets the collection status as its wire value: 0 Pending, 1 PartiallyClaimed, 2 Claimed.
    /// </summary>
    public byte Status { get; set; }

    /// <summary>
    /// Gets or sets the time the collection was created, in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the time the collection last changed, in UTC.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the version, incremented on every change. Clients use it to detect stale entries.
    /// </summary>
    public int Version { get; set; }
}
