// <copyright file="AuctionCollectionTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// One mailbox entry: a delivery obligation of an account.
/// </summary>
/// <param name="CollectionNumber">The collection number on the wire.</param>
/// <param name="Kind">The collection kind as its wire value.</param>
/// <param name="Status">The collection status as its wire value.</param>
/// <param name="Currency">The currency mode as its wire value.</param>
/// <param name="OriginalScalar">The original Zen or jewel amount, or <c>null</c> for fruit and item collections.</param>
/// <param name="RemainingScalar">The remaining Zen or jewel amount, or <c>null</c>.</param>
/// <param name="OriginalFruits">The original fruit amounts, or <c>null</c>.</param>
/// <param name="RemainingFruits">The remaining fruit amounts, or <c>null</c>.</param>
/// <param name="SourceListingNumber">The listing the collection came from, on the wire.</param>
/// <param name="ItemId">The item of an item collection, or <c>null</c>.</param>
/// <param name="Version">The collection version.</param>
public readonly record struct AuctionCollectionRow(
    long CollectionNumber,
    byte Kind,
    byte Status,
    byte Currency,
    long? OriginalScalar,
    long? RemainingScalar,
    FruitQuantities? OriginalFruits,
    FruitQuantities? RemainingFruits,
    long SourceListingNumber,
    Guid? ItemId,
    int Version);

/// <summary>
/// One page of mailbox entries, with the total number of entries.
/// </summary>
/// <param name="Rows">The entries on this page.</param>
/// <param name="TotalCount">The total number of entries.</param>
public readonly record struct AuctionCollectionPage(IReadOnlyList<AuctionCollectionRow> Rows, int TotalCount);
