// <copyright file="AuctionBuyoutTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// The outcome of a buyout.
/// </summary>
public enum AuctionBuyoutOutcome
{
    /// <summary>
    /// The listing was bought out.
    /// </summary>
    Bought,

    /// <summary>
    /// The operation was already processed; nothing was bought again.
    /// </summary>
    Replayed,

    /// <summary>
    /// The listing does not exist.
    /// </summary>
    ListingNotFound,

    /// <summary>
    /// The listing changed since the client last saw it.
    /// </summary>
    StaleListing,

    /// <summary>
    /// The listing is not active, or has already expired.
    /// </summary>
    NotActive,

    /// <summary>
    /// The buyer is the seller.
    /// </summary>
    SelfBuy,

    /// <summary>
    /// The listing has no buyout price.
    /// </summary>
    BuyoutUnavailable,

    /// <summary>
    /// The buyer does not have enough Zen for the amount still to pay.
    /// </summary>
    InsufficientCurrency,

    /// <summary>
    /// The listing's currency cannot yet be debited (jewel tender is not implemented).
    /// </summary>
    UnsupportedCurrency,

    /// <summary>
    /// The character does not belong to the requesting account.
    /// </summary>
    CharacterNotOwned,
}

/// <summary>
/// The result of a buyout.
/// </summary>
/// <param name="Outcome">The outcome.</param>
public readonly record struct AuctionBuyoutResult(AuctionBuyoutOutcome Outcome);

/// <summary>
/// A buyout request. The success fee is computed by the domain rules before this.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="BuyerAccountId">The buyer's account.</param>
/// <param name="BuyerCharacterId">The buyer's character.</param>
/// <param name="ListingId">The listing to buy out.</param>
/// <param name="ExpectedVersion">The listing version the client last saw.</param>
/// <param name="SuccessFee">The success fee charged against the sale, from the domain rules.</param>
/// <param name="BuyerIp">The buyer's IP address.</param>
/// <param name="Now">The time of the buyout, in UTC.</param>
public readonly record struct BuyoutDraft(
    Guid OperationId,
    Guid BuyerAccountId,
    Guid BuyerCharacterId,
    Guid ListingId,
    int ExpectedVersion,
    long SuccessFee,
    string? BuyerIp,
    DateTimeOffset Now);
