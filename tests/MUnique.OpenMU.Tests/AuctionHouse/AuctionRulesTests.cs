// <copyright file="AuctionRulesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the pure Auction House rules: fees, durations, price ranges, bid increments and anti-sniping.
/// </summary>
[TestFixture]
public class AuctionRulesTests
{
    private static readonly DateTimeOffset OriginalEndsAt = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies the default page contains three client viewports, so the Browse scrollbar represents real rows.
    /// </summary>
    [Test]
    public void DefaultPageSizeSupportsScrollableBrowseResults()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new AuctionHouseConfiguration().PageSize, Is.EqualTo(24));
            Assert.That(new AuctionHouseOptions().PageSize, Is.EqualTo(24));
        });
    }

    /// <summary>
    /// Verifies that fees are rounded up with a minimum of one unit of the charged currency.
    /// </summary>
    /// <param name="amount">The charged amount.</param>
    /// <param name="basisPoints">The fee rate in basis points.</param>
    /// <param name="expectedFee">The expected fee.</param>
    [TestCase(1000L, 100, 10L)]
    [TestCase(1L, 100, 1L)]
    [TestCase(150L, 100, 2L)]
    [TestCase(3L, 500, 1L)]
    [TestCase(10000L, 500, 500L)]
    [TestCase(2000000000L, 100, 20000000L)]
    public void FeeRoundsUpWithMinimumOfOne(long amount, int basisPoints, long expectedFee)
    {
        Assert.That(AuctionRules.CalculateFee(amount, basisPoints), Is.EqualTo(expectedFee));
    }

    /// <summary>
    /// Verifies that a zero amount is never charged a fee.
    /// </summary>
    [Test]
    public void ZeroAmountHasNoFee()
    {
        Assert.That(AuctionRules.CalculateFee(0L, 100), Is.EqualTo(0L));
    }

    /// <summary>
    /// Verifies the approved duration set and its default.
    /// </summary>
    /// <param name="hours">The requested duration in hours.</param>
    /// <param name="isValid">Whether the duration is allowed.</param>
    [TestCase(6, true)]
    [TestCase(12, true)]
    [TestCase(24, true)]
    [TestCase(48, true)]
    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(7, false)]
    [TestCase(72, false)]
    public void DurationIsOneOfTheApprovedHours(int hours, bool isValid)
    {
        Assert.That(AuctionRules.IsValidDuration(hours), Is.EqualTo(isValid));
        Assert.That(AuctionRules.DefaultDurationHours, Is.EqualTo(24));
    }

    /// <summary>
    /// Verifies the per-currency scalar price boundaries.
    /// </summary>
    /// <param name="currency">The currency mode.</param>
    /// <param name="price">The starting price.</param>
    /// <param name="isValid">Whether the price is in range.</param>
    [TestCase(AuctionCurrencyMode.Zen, 999L, false)]
    [TestCase(AuctionCurrencyMode.Zen, 1000L, true)]
    [TestCase(AuctionCurrencyMode.Zen, 2000000000L, true)]
    [TestCase(AuctionCurrencyMode.Zen, 2000000001L, false)]
    [TestCase(AuctionCurrencyMode.Chaos, 1L, false)]
    [TestCase(AuctionCurrencyMode.Chaos, 2L, true)]
    [TestCase(AuctionCurrencyMode.Harmony, 10000L, true)]
    [TestCase(AuctionCurrencyMode.Harmony, 10001L, false)]
    public void PriceIsWithinTheCurrencyRange(AuctionCurrencyMode currency, long price, bool isValid)
    {
        Assert.That(AuctionRules.IsPriceInRange(currency, price), Is.EqualTo(isValid));
    }

    /// <summary>
    /// Verifies the first bid must meet the starting price, and that later Zen and jewel bids
    /// follow the approved minimum increments.
    /// </summary>
    /// <param name="currency">The currency mode.</param>
    /// <param name="currentBid">The current bid, or zero when none exists.</param>
    /// <param name="expectedMinimum">The minimum acceptable next bid.</param>
    [TestCase(AuctionCurrencyMode.Zen, 0L, 1000L)]
    [TestCase(AuctionCurrencyMode.Zen, 10000L, 11000L)]
    [TestCase(AuctionCurrencyMode.Zen, 100000L, 105000L)]
    [TestCase(AuctionCurrencyMode.Chaos, 0L, 2L)]
    [TestCase(AuctionCurrencyMode.Chaos, 10L, 11L)]
    [TestCase(AuctionCurrencyMode.Chaos, 100L, 105L)]
    public void MinimumNextBidFollowsIncrementRules(AuctionCurrencyMode currency, long currentBid, long expectedMinimum)
    {
        var startingPrice = currency == AuctionCurrencyMode.Zen ? 1000L : 2L;
        Assert.That(AuctionRules.GetMinimumBid(currency, startingPrice, currentBid), Is.EqualTo(expectedMinimum));
    }

    /// <summary>
    /// Verifies a bid with more than two minutes remaining does not move the expiry.
    /// </summary>
    [Test]
    public void BidWithPlentyOfTimeLeftKeepsExpiry()
    {
        var bidTime = OriginalEndsAt.AddMinutes(-10);

        var endsAt = AuctionRules.ApplyAntiSniping(OriginalEndsAt, OriginalEndsAt, bidTime);

        Assert.That(endsAt, Is.EqualTo(OriginalEndsAt));
    }

    /// <summary>
    /// Verifies a bid with less than two minutes remaining extends expiry to two minutes after the bid.
    /// </summary>
    [Test]
    public void LateBidExtendsExpiryToTwoMinutesAfterTheBid()
    {
        var bidTime = OriginalEndsAt.AddMinutes(-1);

        var endsAt = AuctionRules.ApplyAntiSniping(OriginalEndsAt, OriginalEndsAt, bidTime);

        Assert.That(endsAt, Is.EqualTo(bidTime.AddMinutes(2)));
    }

    /// <summary>
    /// Verifies that repeated late bids can never extend expiry beyond thirty minutes after the original end.
    /// </summary>
    [Test]
    public void ExtensionIsCappedAtThirtyMinutesAfterOriginalEnd()
    {
        var endsAt = OriginalEndsAt.AddMinutes(29);
        var lateBid = endsAt.AddMinutes(-1);

        endsAt = AuctionRules.ApplyAntiSniping(endsAt, OriginalEndsAt, lateBid);
        Assert.That(endsAt, Is.EqualTo(OriginalEndsAt.AddMinutes(30)));

        var laterBid = endsAt.AddMinutes(-1);
        endsAt = AuctionRules.ApplyAntiSniping(endsAt, OriginalEndsAt, laterBid);
        Assert.That(endsAt, Is.EqualTo(OriginalEndsAt.AddMinutes(30)));
    }
}
