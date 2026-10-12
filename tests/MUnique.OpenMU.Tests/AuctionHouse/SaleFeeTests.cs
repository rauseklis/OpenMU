// <copyright file="SaleFeeTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the flat sale fee: one fixed charge per completed sale, held when the listing is created, consumed when
/// the item sells and returned when it does not.
/// </summary>
[TestFixture]
public class SaleFeeTests
{
    /// <summary>
    /// Verifies the approved default: 100,000 Zen.
    /// </summary>
    [Test]
    public void DefaultFeeIsOneHundredThousandZen()
    {
        Assert.That(SaleFee.Default.Currency, Is.EqualTo(SaleFeeCurrency.Zen));
        Assert.That(SaleFee.Default.Amount, Is.EqualTo(100_000));
    }

    /// <summary>
    /// Verifies that a fee cannot be negative, and that zero turns the fee off.
    /// </summary>
    [Test]
    public void FeeAmountCannotBeNegative()
    {
        Assert.That(new SaleFee(SaleFeeCurrency.Zen, 0).IsValid, Is.True);
        Assert.That(new SaleFee(SaleFeeCurrency.Zen, 1).IsValid, Is.True);
        Assert.That(new SaleFee(SaleFeeCurrency.Zen, -1).IsValid, Is.False);
    }

    /// <summary>
    /// Verifies that a charged fee starts held and a free listing holds nothing.
    /// </summary>
    [Test]
    public void ChargedFeeStartsHeldAndFreeListingHoldsNothing()
    {
        Assert.That(SaleFeePolicy.InitialState(new SaleFee(SaleFeeCurrency.Zen, 100_000)), Is.EqualTo(SaleFeeState.Held));
        Assert.That(SaleFeePolicy.InitialState(new SaleFee(SaleFeeCurrency.Zen, 0)), Is.EqualTo(SaleFeeState.None));
    }

    /// <summary>
    /// Verifies that the fee is consumed by a sale and refunded by every other ending.
    /// </summary>
    /// <param name="status">The status the listing ended in.</param>
    /// <param name="expected">The expected fee state.</param>
    [TestCase(ListingStatus.Sold, SaleFeeState.Consumed)]
    [TestCase(ListingStatus.Expired, SaleFeeState.Refunded)]
    [TestCase(ListingStatus.Cancelled, SaleFeeState.Refunded)]
    [TestCase(ListingStatus.AdminRemoved, SaleFeeState.Refunded)]
    [TestCase(ListingStatus.Active, SaleFeeState.Held)]
    public void HeldFeeIsConsumedBySaleAndRefundedOtherwise(ListingStatus status, SaleFeeState expected)
    {
        Assert.That(SaleFeePolicy.StateAfter(SaleFeeState.Held, status), Is.EqualTo(expected));
    }

    /// <summary>
    /// Verifies that a fee which was never held, or is already settled, does not change.
    /// </summary>
    [Test]
    public void SettledOrAbsentFeeNeverChanges()
    {
        Assert.That(SaleFeePolicy.StateAfter(SaleFeeState.None, ListingStatus.Sold), Is.EqualTo(SaleFeeState.None));
        Assert.That(SaleFeePolicy.StateAfter(SaleFeeState.Consumed, ListingStatus.Cancelled), Is.EqualTo(SaleFeeState.Consumed));
        Assert.That(SaleFeePolicy.StateAfter(SaleFeeState.Refunded, ListingStatus.Sold), Is.EqualTo(SaleFeeState.Refunded));
    }

    /// <summary>
    /// Verifies that only a refunded fee is owed back to the seller.
    /// </summary>
    [Test]
    public void OnlyARefundedFeeIsOwedBack()
    {
        Assert.That(SaleFeePolicy.IsRefundOwed(SaleFeeState.Refunded), Is.True);
        Assert.That(SaleFeePolicy.IsRefundOwed(SaleFeeState.Held), Is.False);
        Assert.That(SaleFeePolicy.IsRefundOwed(SaleFeeState.Consumed), Is.False);
        Assert.That(SaleFeePolicy.IsRefundOwed(SaleFeeState.None), Is.False);
    }
}
