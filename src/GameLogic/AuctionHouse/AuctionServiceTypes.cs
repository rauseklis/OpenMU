// <copyright file="AuctionServiceTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// The outcome of a service request. Values match the wire result catalog, so the packet handler converts them directly.
/// </summary>
public enum AuctionResultCode
{
    /// <summary>
    /// The Success outcome.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The Disabled outcome.
    /// </summary>
    Disabled = 1,

    /// <summary>
    /// The NotAuthenticated outcome.
    /// </summary>
    NotAuthenticated = 2,

    /// <summary>
    /// The StateRestricted outcome.
    /// </summary>
    StateRestricted = 3,

    /// <summary>
    /// The LevelTooLow outcome.
    /// </summary>
    LevelTooLow = 4,

    /// <summary>
    /// The AccountTooNew outcome.
    /// </summary>
    AccountTooNew = 5,

    /// <summary>
    /// The AccountRestricted outcome.
    /// </summary>
    AccountRestricted = 6,

    /// <summary>
    /// The RateLimited outcome.
    /// </summary>
    RateLimited = 7,

    /// <summary>
    /// The InvalidRequest outcome.
    /// </summary>
    InvalidRequest = 8,

    /// <summary>
    /// The UnsupportedCurrency outcome.
    /// </summary>
    UnsupportedCurrency = 9,

    /// <summary>
    /// The CurrencyConfigurationInvalid outcome.
    /// </summary>
    CurrencyConfigurationInvalid = 10,

    /// <summary>
    /// The InvalidDuration outcome.
    /// </summary>
    InvalidDuration = 11,

    /// <summary>
    /// The InvalidPrice outcome.
    /// </summary>
    InvalidPrice = 12,

    /// <summary>
    /// The InvalidFruitBasket outcome.
    /// </summary>
    InvalidFruitBasket = 13,

    /// <summary>
    /// The ItemNotFound outcome.
    /// </summary>
    ItemNotFound = 14,

    /// <summary>
    /// The ItemNotEligible outcome.
    /// </summary>
    ItemNotEligible = 15,

    /// <summary>
    /// The ListingLimitReached outcome.
    /// </summary>
    ListingLimitReached = 16,

    /// <summary>
    /// The InsufficientCurrency outcome.
    /// </summary>
    InsufficientCurrency = 17,

    /// <summary>
    /// The ListingNotFound outcome.
    /// </summary>
    ListingNotFound = 18,

    /// <summary>
    /// The ListingNotActive outcome.
    /// </summary>
    ListingNotActive = 19,

    /// <summary>
    /// The ListingExpired outcome.
    /// </summary>
    ListingExpired = 20,

    /// <summary>
    /// The StaleListing outcome.
    /// </summary>
    StaleListing = 21,

    /// <summary>
    /// The SelfTrade outcome.
    /// </summary>
    SelfTrade = 22,

    /// <summary>
    /// The AlreadyHighestBidder outcome.
    /// </summary>
    AlreadyHighestBidder = 23,

    /// <summary>
    /// The BidTooLow outcome.
    /// </summary>
    BidTooLow = 24,

    /// <summary>
    /// The BuyoutUnavailable outcome.
    /// </summary>
    BuyoutUnavailable = 25,

    /// <summary>
    /// The CannotCancelWithBid outcome.
    /// </summary>
    CannotCancelWithBid = 26,

    /// <summary>
    /// The CollectionNotFound outcome.
    /// </summary>
    CollectionNotFound = 27,

    /// <summary>
    /// The InvalidClaimQuantity outcome.
    /// </summary>
    InvalidClaimQuantity = 28,

    /// <summary>
    /// The InventoryFull outcome.
    /// </summary>
    InventoryFull = 29,

    /// <summary>
    /// The MoneyLimit outcome.
    /// </summary>
    MoneyLimit = 30,

    /// <summary>
    /// The AlreadyProcessed outcome.
    /// </summary>
    AlreadyProcessed = 31,

    /// <summary>
    /// The ServiceUnavailable outcome.
    /// </summary>
    ServiceUnavailable = 32,
}

/// <summary>
/// The result of opening the window.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="PendingMailbox">The number of collections the account has not finished claiming.</param>
/// <param name="ServerTime">The server time, in UTC.</param>
/// <param name="PageSize">The configured number of listings per page.</param>
public readonly record struct AuctionOpenResult(AuctionResultCode Result, int PendingMailbox, DateTimeOffset ServerTime, int PageSize);

/// <summary>
/// The result of a browse request.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="Page">The listings on the page; empty when the request failed.</param>
/// <param name="ServerTime">The server time, in UTC.</param>
public readonly record struct AuctionBrowseResult(AuctionResultCode Result, AuctionListingPage Page, DateTimeOffset ServerTime);

/// <summary>
/// The result of a detail request.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="Listing">The listing; <c>null</c> when the request failed.</param>
/// <param name="ServerTime">The server time, in UTC.</param>
public readonly record struct AuctionDetailResult(AuctionResultCode Result, AuctionListingDetail? Listing, DateTimeOffset ServerTime);

/// <summary>
/// A request to list one inventory item for sale.
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
/// <param name="DurationHours">The listing duration in hours.</param>
public readonly record struct AuctionListItemRequest(
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
    int DurationHours);

/// <summary>
/// The result of listing an inventory item.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="ListingId">The listing; an empty identifier when the listing was refused.</param>
/// <param name="ListingNumber">The listing number; zero when the listing was refused.</param>
/// <param name="ServerTime">The server time, in UTC.</param>
public readonly record struct AuctionListItemResult(AuctionResultCode Result, Guid ListingId, long ListingNumber, DateTimeOffset ServerTime);

