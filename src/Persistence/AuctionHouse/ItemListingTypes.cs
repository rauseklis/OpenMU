// <copyright file="ItemListingTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The outcome of listing an inventory item.
/// </summary>
public enum AuctionItemListingOutcome
{
    /// <summary>The listing was created and the item left the inventory.</summary>
    Created,

    /// <summary>The operation was already processed; the stored listing is returned.</summary>
    Replayed,

    /// <summary>No item is in the requested inventory slot.</summary>
    ItemNotFound,

    /// <summary>The item failed the eligibility check, which ran inside the transaction.</summary>
    NotEligible,

    /// <summary>The seller already has the maximum number of active listings.</summary>
    ListingLimitReached,

    /// <summary>The character cannot pay the listing fee.</summary>
    InsufficientCurrency,

    /// <summary>The item already has an active listing or a pending collection.</summary>
    ItemAlreadyListed,

    /// <summary>The note is not valid UTF-8 text within its byte limit.</summary>
    InvalidNote,

    /// <summary>The character does not belong to the requesting account.</summary>
    CharacterNotOwned,
}

/// <summary>
/// The result of listing an inventory item.
/// </summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="ListingId">The listing; an empty identifier when the listing was refused.</param>
/// <param name="ListingNumber">The listing number; zero when the listing was refused.</param>
public readonly record struct AuctionItemListingResult(AuctionItemListingOutcome Outcome, Guid ListingId, long ListingNumber);

/// <summary>
/// The facts about an item in the inventory which the eligibility check decides on. They are read inside the transaction.
/// </summary>
/// <param name="ItemId">The item.</param>
/// <param name="IsTradable">Whether the item definition is tradable.</param>
/// <param name="IsPersonalStoreSellable">Whether the item definition may be sold in a personal store.</param>
/// <param name="IsBoundToCharacter">Whether the item definition is bound to its character.</param>
/// <param name="IsQuestItem">Whether the item definition is a quest item.</param>
/// <param name="Durability">The durability, rounded up.</param>
/// <param name="InventorySlot">The slot the item occupies.</param>
/// <param name="IsAlreadyReferenced">Whether an active listing or a pending collection already references the item.</param>
public readonly record struct ItemListingFacts(
    Guid ItemId,
    bool IsTradable,
    bool IsPersonalStoreSellable,
    bool IsBoundToCharacter,
    bool IsQuestItem,
    int Durability,
    byte InventorySlot,
    bool IsAlreadyReferenced,
    int ItemGroup,
    int ItemNumber);

/// <summary>
/// The values for listing an inventory item. The item itself is selected by its slot inside the transaction.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="AccountId">The seller's account.</param>
/// <param name="CharacterId">The seller's character, whose inventory holds the item.</param>
/// <param name="SellerName">The seller's character name.</param>
/// <param name="SellerIp">The seller's IP address.</param>
/// <param name="InventorySlot">The inventory slot of the item.</param>
/// <param name="Currency">The currency mode as its wire value.</param>
/// <param name="Category">The category as its wire value.</param>
/// <param name="StartingPrice">The scalar starting price.</param>
/// <param name="BuyoutPrice">The buyout price, or <c>null</c> when there is none.</param>
/// <param name="CreatedAt">The creation time, in UTC.</param>
/// <param name="EndsAt">The expiry, in UTC.</param>
/// <param name="ListingFee">The listing fee, charged in the listing currency.</param>
/// <param name="ListingLimitPerAccount">The maximum number of active listings of the account.</param>
/// <param name="ListingLimitPerCharacter">The maximum number of active listings of the character.</param>
/// <param name="SaleFee">The flat sale fee in Zen, held until the listing ends. Zero charges none.</param>
/// <param name="StartingBasket">The starting price of a mixed-currency auction, or <c>null</c> for a buy-now listing or a single-currency listing.</param>
/// <param name="BuyoutBasket">The buyout (or buy-now) price of a mixed-currency listing, or <c>null</c>.</param>
public readonly record struct ItemListingDraft(
    Guid OperationId,
    Guid AccountId,
    Guid CharacterId,
    string SellerName,
    string? SellerIp,
    byte InventorySlot,
    byte Currency,
    byte Category,
    long StartingPrice,
    long? BuyoutPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset EndsAt,
    long ListingFee,
    int ListingLimitPerAccount,
    int ListingLimitPerCharacter,
    FruitBasket? StartingFruits = null,
    FruitBasket? FeeFruits = null,
    FruitBasket? BuyoutFruits = null,
    string? Note = null,
    long SaleFee = 0,
    PriceBasket? StartingBasket = null,
    PriceBasket? BuyoutBasket = null,
    byte GameServerId = 0);
