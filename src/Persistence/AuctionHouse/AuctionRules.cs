// <copyright file="AuctionRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The pure Auction House rules. These carry no state and depend on no persistence or packet types,
/// so they can be verified in isolation.
/// </summary>
public static class AuctionRules
{
    /// <summary>
    /// The listing duration offered when the seller does not choose one.
    /// </summary>
    public const int DefaultDurationHours = 24;

    public const int MinimumZenPrice = 1000;
    public const int MaximumZenPrice = 2_000_000_000;
    private const int MinimumJewelPrice = 2;
    private const int MaximumJewelPrice = 10_000;
    public const int MinimumZenBidIncrement = 1000;
    private const int MinimumJewelBidIncrement = 1;
    public const int BidIncrementPercent = 5;
    private const int BasisPointsDivisor = 10_000;
    private static readonly TimeSpan AntiSnipingWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaximumExtension = TimeSpan.FromMinutes(30);
    private static readonly int[] DurationsInHours = { 6, 12, 24, 48 };

    /// <summary>
    /// Calculates a fee in whole units. The fee is rounded up, and any nonzero amount is charged at least one unit.
    /// </summary>
    /// <param name="amount">The amount the fee applies to.</param>
    /// <param name="basisPoints">The fee rate in basis points (100 is 1%).</param>
    /// <returns>The fee in whole units.</returns>
    public static long CalculateFee(long amount, int basisPoints)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var rounded = (amount * basisPoints + BasisPointsDivisor - 1) / BasisPointsDivisor;
        return Math.Max(1, rounded);
    }

    /// <summary>
    /// Determines whether a listing duration is one of the approved durations.
    /// </summary>
    /// <param name="hours">The requested duration in hours.</param>
    /// <returns><c>true</c> if the duration is allowed.</returns>
    public static bool IsValidDuration(int hours) => Array.IndexOf(DurationsInHours, hours) >= 0;

    /// <summary>
    /// Determines whether a scalar starting price is within the range of its currency.
    /// Fruit baskets are validated per component and are not handled here.
    /// </summary>
    /// <param name="currency">The currency mode.</param>
    /// <param name="price">The scalar price in whole units.</param>
    /// <returns><c>true</c> if the price is within range.</returns>
    public static bool IsPriceInRange(AuctionCurrencyMode currency, long price)
    {
        return currency switch
        {
            AuctionCurrencyMode.Zen => price >= MinimumZenPrice && price <= MaximumZenPrice,
            AuctionCurrencyMode.Fruits => false,
            _ => price >= MinimumJewelPrice && price <= MaximumJewelPrice,
        };
    }

    /// <summary>
    /// Calculates the lowest acceptable next scalar bid. Without a current bid, the starting price is the minimum.
    /// </summary>
    /// <param name="currency">The currency mode.</param>
    /// <param name="startingPrice">The listing's starting price.</param>
    /// <param name="currentBid">The current highest bid, or zero when there is none.</param>
    /// <returns>The minimum acceptable bid.</returns>
    public static long GetMinimumBid(AuctionCurrencyMode currency, long startingPrice, long currentBid)
    {
        if (currentBid <= 0)
        {
            return startingPrice;
        }

        var minimumIncrement = currency == AuctionCurrencyMode.Zen ? MinimumZenBidIncrement : MinimumJewelBidIncrement;
        var percentIncrement = (currentBid * BidIncrementPercent + 99) / 100;
        return currentBid + Math.Max(minimumIncrement, percentIncrement);
    }

    /// <summary>
    /// Applies anti-sniping. A bid with less than two minutes remaining moves expiry to two minutes after the bid,
    /// but never beyond thirty minutes after the original end, and never earlier than the current expiry.
    /// </summary>
    /// <param name="endsAt">The current expiry.</param>
    /// <param name="originalEndsAt">The expiry the listing was created with.</param>
    /// <param name="bidTime">The time of the bid.</param>
    /// <returns>The expiry after the bid.</returns>
    public static DateTimeOffset ApplyAntiSniping(DateTimeOffset endsAt, DateTimeOffset originalEndsAt, DateTimeOffset bidTime)
    {
        if (endsAt - bidTime >= AntiSnipingWindow)
        {
            return endsAt;
        }

        var cap = originalEndsAt + MaximumExtension;
        var candidate = bidTime + AntiSnipingWindow;
        if (candidate > cap)
        {
            candidate = cap;
        }

        return candidate > endsAt ? candidate : endsAt;
    }
}
