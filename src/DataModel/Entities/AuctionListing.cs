// <copyright file="AuctionListing.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// A player listing in the Auction House. The item is held in escrow by the listing until it is sold, cancelled or expires.
/// Statuses, currency modes and categories are stored as their wire values, which the GameLogic layer maps to enums.
/// </summary>
[AggregateRoot]
public class AuctionListing
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the 64-bit listing number used on the wire. Assigned by the database.
    /// </summary>
    public long ListingNumber { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the game server this listing belongs to. Listings are only visible on, and can only
    /// be bought on, the game server they were created on.
    /// </summary>
    public byte GameServerId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the listed item. The item's identity is preserved.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the seller's account.
    /// </summary>
    public Guid SellerAccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the seller's character.
    /// </summary>
    public Guid SellerCharacterId { get; set; }

    /// <summary>
    /// Gets or sets the seller's character name at the time of listing.
    /// </summary>
    public string SellerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the seller's IP address at the time of listing.
    /// </summary>
    public string? SellerIp { get; set; }

    /// <summary>
    /// Gets or sets the listing status as its wire value.
    /// </summary>
    public byte Status { get; set; }

    /// <summary>
    /// Gets or sets the currency mode as its wire value.
    /// </summary>
    public byte Currency { get; set; }

    /// <summary>
    /// Gets or sets the category snapshot as its wire value.
    /// </summary>
    public byte Category { get; set; }

    /// <summary>
    /// Gets or sets the sub-category snapshot within <see cref="Category"/>. 255 means the listing has not been classified into
    /// a sub-category yet; the maintenance task fills it in.
    /// </summary>
    public byte SubCategory { get; set; } = 255;

    /// <summary>
    /// Gets or sets the scalar starting price, used by Zen and jewel modes.
    /// </summary>
    public long StartingPrice { get; set; }

    /// <summary>
    /// Gets or sets the scalar buyout price, used by Zen and jewel modes.
    /// </summary>
    public long? BuyoutPrice { get; set; }

    /// <summary>
    /// Gets or sets the scalar current bid, used by Zen and jewel modes.
    /// </summary>
    public long? CurrentBid { get; set; }

    /// <summary>
    /// Gets or sets the starting fruit basket: Strength, Agility, Vitality, Energy and Command quantities.
    /// </summary>
    public long StartingStrength { get; set; }

    /// <inheritdoc cref="StartingStrength" />
    public long StartingAgility { get; set; }

    /// <inheritdoc cref="StartingStrength" />
    public long StartingVitality { get; set; }

    /// <inheritdoc cref="StartingStrength" />
    public long StartingEnergy { get; set; }

    /// <inheritdoc cref="StartingStrength" />
    public long StartingCommand { get; set; }

    /// <summary>
    /// Gets or sets the fruit buyout basket, used only by the Fruits mode. Null when there is no buyout.
    /// </summary>
    public long? BuyoutStrength { get; set; }

    /// <inheritdoc cref="BuyoutStrength" />
    public long? BuyoutAgility { get; set; }

    /// <inheritdoc cref="BuyoutStrength" />
    public long? BuyoutVitality { get; set; }

    /// <inheritdoc cref="BuyoutStrength" />
    public long? BuyoutEnergy { get; set; }

    /// <inheritdoc cref="BuyoutStrength" />
    public long? BuyoutCommand { get; set; }

    /// <summary>
    /// Gets or sets the current fruit bid basket, used only by the Fruits mode. Null when there is no bid.
    /// </summary>
    public long? CurrentBidStrength { get; set; }

    /// <inheritdoc cref="CurrentBidStrength" />
    public long? CurrentBidAgility { get; set; }

    /// <inheritdoc cref="CurrentBidStrength" />
    public long? CurrentBidVitality { get; set; }

    /// <inheritdoc cref="CurrentBidStrength" />
    public long? CurrentBidEnergy { get; set; }

    /// <inheritdoc cref="CurrentBidStrength" />
    public long? CurrentBidCommand { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account which holds the current highest bid.
    /// </summary>
    public Guid? CurrentBidderAccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the character which holds the current highest bid.
    /// </summary>
    public Guid? CurrentBidderCharacterId { get; set; }

    /// <summary>
    /// Gets or sets the time the listing was created, in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the expiry the listing was created with, in UTC.
    /// </summary>
    public DateTimeOffset OriginalEndsAt { get; set; }

    /// <summary>
    /// Gets or sets the flat sale fee held when the listing was created, in Zen. Zero when no fee was charged.
    /// </summary>
    public long SaleFeeHeld { get; set; }

    /// <summary>
    /// Gets or sets the state of the held fee as its wire value: 0 none, 1 held, 2 consumed by a sale, 3 refunded.
    /// </summary>
    public byte SaleFeeState { get; set; }

    /// <summary>
    /// Gets or sets the starting price of a mixed-currency listing as 13 comma-separated quantities in price-kind order.
    /// <c>null</c> for single-currency listings and for buy-now listings, which have no starting price.
    /// </summary>
    public string? PriceVector { get; set; }

    /// <summary>
    /// Gets or sets the buyout price of a mixed-currency listing as 13 comma-separated quantities, or <c>null</c>.
    /// </summary>
    public string? BuyoutVector { get; set; }

    /// <summary>
    /// Gets or sets the current high bid of a mixed-currency listing as 13 comma-separated quantities, or <c>null</c>.
    /// </summary>
    public string? CurrentBidVector { get; set; }

    /// <summary>
    /// Gets or sets a bit mask with one bit (the price kind's number) for every kind the listing's price uses. It serves
    /// the currency filter of browse. Zero for single-currency listings.
    /// </summary>
    public int KindMask { get; set; }

    /// <summary>
    /// Gets or sets the current expiry, in UTC. Anti-sniping may extend it up to the cap.
    /// </summary>
    public DateTimeOffset EndsAt { get; set; }

    /// <summary>
    /// Gets or sets the time of the last bid, in UTC.
    /// </summary>
    public DateTimeOffset? LastBidAt { get; set; }

    /// <summary>
    /// Gets or sets the time the listing was finalized, in UTC.
    /// </summary>
    public DateTimeOffset? FinalizedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of bids placed.
    /// </summary>
    public int BidCount { get; set; }

    /// <summary>
    /// Gets or sets the optional note, at most 80 encoded UTF-8 bytes.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets the version, incremented on every mutation. Clients use it to detect stale listings.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the search snapshot of the item definition: group, number and level.
    /// </summary>
    public int ItemGroup { get; set; }

    /// <inheritdoc cref="ItemGroup" />
    public int ItemNumber { get; set; }

    /// <inheritdoc cref="ItemGroup" />
    public int ItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the item definition at listing time.
    /// </summary>
    public Guid? ItemDefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the base-character-class usability mask at listing time. Zero means unrestricted.
    /// </summary>
    public long ClassMask { get; set; }

    /// <summary>
    /// Gets or sets the auction item-option flags at listing time.
    /// </summary>
    public int OptionFlags { get; set; }

    /// <summary>
    /// Gets or sets the ordinary additional-option level at listing time.
    /// </summary>
    public int OptionLevel { get; set; }

    /// <summary>
    /// Gets or sets the normalized item name used for case-insensitive substring search.
    /// </summary>
    public string NormalizedName { get; set; } = string.Empty;
}
