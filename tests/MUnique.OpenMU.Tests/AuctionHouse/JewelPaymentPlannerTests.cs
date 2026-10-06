// <copyright file="JewelPaymentPlannerTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies jewel pack normalization and deterministic payment planning.
/// </summary>
[TestFixture]
public class JewelPaymentPlannerTests
{
    /// <summary>
    /// Verifies the packed jewel levels map to their loose-unit values, and that other levels are rejected.
    /// </summary>
    /// <param name="level">The packed item level.</param>
    /// <param name="units">The expected loose-unit value.</param>
    [TestCase(0, 10)]
    [TestCase(1, 20)]
    [TestCase(2, 30)]
    public void PackLevelMapsToLooseUnits(int level, int units)
    {
        Assert.That(JewelPackRules.GetPackUnits(level), Is.EqualTo(units));
    }

    /// <summary>
    /// Verifies that a level outside the approved packed mapping is rejected rather than treated as tender.
    /// </summary>
    [TestCase(3)]
    [TestCase(-1)]
    public void UnknownPackLevelIsRejected(int level)
    {
        Assert.That(() => JewelPackRules.GetPackUnits(level), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    /// <summary>
    /// Verifies that an exact payment with loose jewels uses the lowest slots and gives no change.
    /// </summary>
    [Test]
    public void ExactLooseJewelsUseLowestSlots()
    {
        var candidates = new[] { new JewelStack(8, 1), new JewelStack(5, 1), new JewelStack(6, 1), new JewelStack(7, 1) };

        var payment = JewelPaymentPlanner.Plan(3, candidates);

        Assert.That(payment, Is.Not.Null);
        Assert.That(payment!.Value.Slots, Is.EqualTo(new[] { 5, 6, 7 }));
        Assert.That(payment.Value.ChangeUnits, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies that one pack is preferred over many loose jewels worth the same amount.
    /// </summary>
    [Test]
    public void SinglePackIsPreferredOverEquivalentLooseJewels()
    {
        var candidates = new List<JewelStack> { new(10, 20) };
        candidates.AddRange(Enumerable.Range(1, 20).Select(slot => new JewelStack(slot, 1)));

        var payment = JewelPaymentPlanner.Plan(20, candidates);

        Assert.That(payment, Is.Not.Null);
        Assert.That(payment!.Value.Slots, Is.EqualTo(new[] { 10 }));
        Assert.That(payment.Value.ChangeUnits, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies that the planner minimizes overpayment, and issues the difference as change.
    /// </summary>
    [Test]
    public void OverpaymentIsMinimizedAndReturnedAsChange()
    {
        var candidates = new[] { new JewelStack(1, 30), new JewelStack(2, 20) };

        var payment = JewelPaymentPlanner.Plan(25, candidates);

        Assert.That(payment, Is.Not.Null);
        Assert.That(payment!.Value.Slots, Is.EqualTo(new[] { 1 }));
        Assert.That(payment.Value.ChangeUnits, Is.EqualTo(5));
    }

    /// <summary>
    /// Verifies that combining packs is chosen when it overpays less than any single pack.
    /// </summary>
    [Test]
    public void CombinationIsChosenWhenItOverpaysLess()
    {
        var candidates = new[] { new JewelStack(1, 30), new JewelStack(2, 20), new JewelStack(3, 1) };

        var payment = JewelPaymentPlanner.Plan(45, candidates);

        Assert.That(payment, Is.Not.Null);
        Assert.That(payment!.Value.Slots, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(payment.Value.ChangeUnits, Is.EqualTo(5));
    }

    /// <summary>
    /// Verifies that equal packs are broken by slot order.
    /// </summary>
    [Test]
    public void EqualPacksAreBrokenByLowestSlot()
    {
        var candidates = new[] { new JewelStack(7, 10), new JewelStack(3, 10) };

        var payment = JewelPaymentPlanner.Plan(10, candidates);

        Assert.That(payment, Is.Not.Null);
        Assert.That(payment!.Value.Slots, Is.EqualTo(new[] { 3 }));
    }

    /// <summary>
    /// Verifies that insufficient total value cannot be paid.
    /// </summary>
    [Test]
    public void InsufficientValueReturnsNull()
    {
        var candidates = new[] { new JewelStack(1, 10), new JewelStack(2, 1) };

        Assert.That(JewelPaymentPlanner.Plan(12, candidates), Is.Null);
    }
}
