// <copyright file="AuctionSearchTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The order of browse results. Values match the wire catalog.
/// </summary>
public enum AuctionSearchSort
{
    /// <summary>
    /// Ascending by effective price (current bid, else starting price). Not valid for fruit baskets.
    /// </summary>
    PriceAscending = 0,

    /// <summary>
    /// Descending by effective price. Not valid for fruit baskets.
    /// </summary>
    PriceDescending = 1,

    /// <summary>
    /// The earliest expiry first.
    /// </summary>
    EndingSoonest = 2,

    /// <summary>
    /// The most recent listing first.
    /// </summary>
    Newest = 3,
}

/// <summary>
/// The searchable item-option flags. Values match the extended item wire format.
/// </summary>
[Flags]
public enum AuctionItemOptionFlags : ushort
{
    None = 0,
    HasOption = 0x01,
    HasLuck = 0x02,
    HasSkill = 0x04,
    HasExcellent = 0x08,
    HasAncient = 0x10,
    HasHarmony = 0x20,
    HasGuardian = 0x40,
    HasSockets = 0x80,
}

/// <summary>
/// A browse request. When no currency is selected, listings of every currency are returned. Price ordering and price-range
/// filters require a single currency because values in different currencies cannot be compared honestly.
/// </summary>
/// <param name="Currency">The currency mode as its wire value, or <c>null</c> for every currency.</param>
/// <param name="Category">The category filter, or <c>null</c> for all categories.</param>
/// <param name="Sort">The order of results.</param>
/// <param name="Page">The page, starting at 1.</param>
/// <param name="PageSize">The number of listings per page.</param>
/// <param name="Name">The normalized neutral-name substring, or <c>null</c> for every name.</param>
/// <param name="LevelMinimum">The minimum item level, or zero when only <paramref name="LevelMaximum"/> is relevant.</param>
/// <param name="LevelMaximum">The maximum item level, or zero for no maximum.</param>
/// <param name="MinimumScalar">The minimum effective scalar price, or zero for no minimum.</param>
/// <param name="MaximumScalar">The maximum effective scalar price, or zero for no maximum.</param>
/// <param name="MinimumFruits">The component-wise minimum effective fruit price.</param>
/// <param name="MaximumFruits">The component-wise maximum effective fruit price.</param>
/// <param name="MaxRemainingHours">The maximum whole hours remaining, or zero for no maximum.</param>
/// <param name="ClassMask">The acceptable base-character-class bits, or zero for every class.</param>
/// <param name="OptionFlags">The item-option flags which must all be present.</param>
/// <param name="SubCategory">The sub-category filter within <paramref name="Category"/>, or <c>null</c> for every sub-category.</param>
public readonly record struct AuctionListingSearch(
    byte? Currency,
    byte? Category = null,
    AuctionSearchSort Sort = AuctionSearchSort.EndingSoonest,
    int Page = 1,
    int PageSize = 8,
    string? Name = null,
    byte LevelMinimum = 0,
    byte LevelMaximum = 0,
    long MinimumScalar = 0,
    long MaximumScalar = 0,
    FruitBasket? MinimumFruits = null,
    FruitBasket? MaximumFruits = null,
    int MaxRemainingHours = 0,
    uint ClassMask = 0,
    AuctionItemOptionFlags OptionFlags = AuctionItemOptionFlags.None,
    byte? SubCategory = null,
    byte? GameServerId = null);

/// <summary>
/// The browse-row summary of a listing.
/// </summary>
public readonly record struct AuctionListingRow(
    Guid ListingId,
    long ListingNumber,
    byte Currency,
    byte Category,
    long StartingPrice,
    long? CurrentBid,
    long? BuyoutPrice,
    DateTimeOffset EndsAt,
    int BidCount,
    Guid SellerAccountId,
    string SellerName,
    int ItemGroup,
    int ItemNumber,
    int ItemLevel,
    Guid ItemId,
    int Version,
    byte Status,
    int OptionFlags,
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

/// <summary>
/// One page of browse results, with the total number of matches.
/// </summary>
/// <param name="Rows">The listings on this page.</param>
/// <param name="TotalCount">The number of matching listings across all pages.</param>
public readonly record struct AuctionListingPage(IReadOnlyList<AuctionListingRow> Rows, int TotalCount);
