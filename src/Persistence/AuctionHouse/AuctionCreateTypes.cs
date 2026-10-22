// <copyright file="AuctionCreateTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// The outcome of a listing creation.
/// </summary>
public enum AuctionCreateOutcome
{
    /// <summary>
    /// The listing was created.
    /// </summary>
    Created,

    /// <summary>
    /// The operation was already processed; the stored outcome is returned and nothing was created.
    /// </summary>
    Replayed,

    /// <summary>
    /// The item already has an active listing.
    /// </summary>
    ItemAlreadyListed,
}

/// <summary>
/// The result of a listing creation.
/// </summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="ListingId">The listing identifier, or an empty identifier when the listing was refused.</param>
/// <param name="ListingNumber">The 64-bit listing number used on the wire, or zero when the listing was refused.</param>
public readonly record struct AuctionCreateResult(AuctionCreateOutcome Outcome, Guid ListingId, long ListingNumber);

/// <summary>
/// The values for a new listing, along with the operation which requested it.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="AccountId">The seller's account.</param>
/// <param name="ItemId">The item to list.</param>
/// <param name="SellerCharacterId">The seller's character.</param>
/// <param name="SellerName">The seller's character name.</param>
/// <param name="SellerIp">The seller's IP address.</param>
/// <param name="Currency">The currency mode as its wire value.</param>
/// <param name="Category">The category as its wire value.</param>
/// <param name="StartingPrice">The scalar starting price.</param>
/// <param name="CreatedAt">The creation time, in UTC.</param>
/// <param name="EndsAt">The expiry, in UTC.</param>
/// <param name="SubCategory">The sub-category, 255 until it has been classified.</param>
public readonly record struct AuctionListingDraft(
    Guid OperationId,
    Guid AccountId,
    Guid ItemId,
    Guid SellerCharacterId,
    string SellerName,
    string? SellerIp,
    byte Currency,
    byte Category,
    long StartingPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset EndsAt,
    long? BuyoutPrice = null,
    byte SubCategory = 255,
    byte GameServerId = 0);
