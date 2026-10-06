// <copyright file="FruitBidDraft.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A fruit-basket bid. The basket is checked against the listing's starting and current baskets inside the transaction.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="BidderAccountId">The bidder's account.</param>
/// <param name="BidderCharacterId">The bidder's character, whose fruits pay the bid.</param>
/// <param name="ListingId">The listing.</param>
/// <param name="ExpectedVersion">The listing version the bidder saw.</param>
/// <param name="Bid">The bid basket.</param>
/// <param name="BidderIp">The bidder's IP address.</param>
/// <param name="Now">The current time, in UTC.</param>
/// <param name="NewEndsAt">The listing's end after anti-sniping.</param>
public readonly record struct FruitBidDraft(
    Guid OperationId,
    Guid BidderAccountId,
    Guid BidderCharacterId,
    Guid ListingId,
    int ExpectedVersion,
    FruitBasket Bid,
    string? BidderIp,
    DateTimeOffset Now,
    DateTimeOffset NewEndsAt);
