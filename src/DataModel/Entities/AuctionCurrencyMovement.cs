// <copyright file="AuctionCurrencyMovement.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// An immutable ledger row for one non-item value movement. Fruit movements use one row per component, so each
/// component is conserved independently.
/// </summary>
[AggregateRoot]
public class AuctionCurrencyMovement
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account whose balance moved.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the operation which caused the movement.
    /// </summary>
    public Guid OperationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the listing involved, if any.
    /// </summary>
    public Guid? ListingId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the bid involved, if any.
    /// </summary>
    public Guid? BidId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the collection involved, if any.
    /// </summary>
    public Guid? CollectionId { get; set; }

    /// <summary>
    /// Gets or sets the currency mode as its wire value.
    /// </summary>
    public byte Currency { get; set; }

    /// <summary>
    /// Gets or sets the fruit component as its wire value (0 Strength to 4 Command), or null for scalar currencies.
    /// </summary>
    public byte? FruitComponent { get; set; }

    /// <summary>
    /// Gets or sets the amount in whole units. Positive is credit, negative is debit.
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// Gets or sets the movement kind as its wire value.
    /// </summary>
    public byte Kind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the inventory item consumed by the movement, if any. This is historical and
    /// is not a cascading foreign key, because fungible tender items can be deleted.
    /// </summary>
    public Guid? SourceItemId { get; set; }

    /// <summary>
    /// Gets or sets the loose-unit value of a consumed packed jewel, if relevant.
    /// </summary>
    public int? PackedUnitValue { get; set; }

    /// <summary>
    /// Gets or sets the time of the movement, in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
