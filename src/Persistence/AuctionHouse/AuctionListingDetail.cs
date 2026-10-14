// <copyright file="AuctionListingDetail.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The detail view of one listing: the fields a player may see, without the seller's network address or bidder identities.
/// </summary>
/// <param name="ListingId">The listing.</param>
/// <param name="Version">The listing version, for optimistic mutations.</param>
/// <param name="Status">The listing status.</param>
/// <param name="Currency">The currency mode.</param>
/// <param name="Category">The category.</param>
/// <param name="ItemId">The auctioned item.</param>
/// <param name="StartingPrice">The starting price.</param>
/// <param name="BuyoutPrice">The buyout price, or <c>null</c> when there is none.</param>
/// <param name="CurrentBid">The current bid, or <c>null</c> when there is none.</param>
/// <param name="EndsAt">When the listing ends, in UTC.</param>
/// <param name="OriginalEndsAt">When the listing first ended, in UTC; anti-sniping extensions are capped from here.</param>
/// <param name="BidCount">The number of bids.</param>
/// <param name="SellerName">The seller's name.</param>
/// <param name="Note">The seller's note.</param>
public sealed record AuctionListingDetail(
    Guid ListingId,
    int Version,
    byte Status,
    byte Currency,
    byte Category,
    Guid ItemId,
    long StartingPrice,
    long? BuyoutPrice,
    long? CurrentBid,
    DateTimeOffset EndsAt,
    DateTimeOffset OriginalEndsAt,
    int BidCount,
    string SellerName,
    string? Note,
    long? StartingStrength,
    long? StartingAgility,
    long? StartingVitality,
    long? StartingEnergy,
    long? StartingCommand,
    long? CurrentBidStrength,
    long? CurrentBidAgility,
    long? CurrentBidVitality,
    long? CurrentBidEnergy,
    long? CurrentBidCommand,
    long? BuyoutStrength,
    long? BuyoutAgility,
    long? BuyoutVitality,
    long? BuyoutEnergy,
    long? BuyoutCommand,
    string? PriceVector = null,
    string? BuyoutVector = null,
    string? CurrentBidVector = null)
{
    /// <summary>The starting basket of a fruit listing.</summary>
    public FruitBasket? StartingFruits => Basket(StartingStrength, StartingAgility, StartingVitality, StartingEnergy, StartingCommand);

    /// <summary>The current bid basket of a fruit listing, or <c>null</c> without a bid.</summary>
    public FruitBasket? CurrentFruits => Basket(CurrentBidStrength, CurrentBidAgility, CurrentBidVitality, CurrentBidEnergy, CurrentBidCommand);

    /// <summary>The buyout basket of a fruit listing, or <c>null</c> when there is none.</summary>
    public FruitBasket? BuyoutFruits => Basket(BuyoutStrength, BuyoutAgility, BuyoutVitality, BuyoutEnergy, BuyoutCommand);

    private static FruitBasket? Basket(long? strength, long? agility, long? vitality, long? energy, long? command)
        => strength is null ? null : new FruitBasket(strength ?? 0, agility ?? 0, vitality ?? 0, energy ?? 0, command ?? 0);
}
