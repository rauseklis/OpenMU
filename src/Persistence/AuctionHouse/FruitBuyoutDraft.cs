// <copyright file="FruitBuyoutDraft.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// A fruit-basket buyout. The listing's buyout basket is paid by the buyer, less any fruits the buyer already escrowed as the high bidder.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="BuyerAccountId">The buyer's account.</param>
/// <param name="BuyerCharacterId">The buyer's character, whose fruits pay the buyout.</param>
/// <param name="ListingId">The listing.</param>
/// <param name="ExpectedVersion">The listing version the buyer saw.</param>
/// <param name="BuyerIp">The buyer's IP address.</param>
/// <param name="Now">The current time, in UTC.</param>
/// <param name="SuccessFeeBasisPoints">The success fee in basis points of each component of the sale.</param>
public readonly record struct FruitBuyoutDraft(
    Guid OperationId,
    Guid BuyerAccountId,
    Guid BuyerCharacterId,
    Guid ListingId,
    int ExpectedVersion,
    string? BuyerIp,
    DateTimeOffset Now,
    int SuccessFeeBasisPoints);
