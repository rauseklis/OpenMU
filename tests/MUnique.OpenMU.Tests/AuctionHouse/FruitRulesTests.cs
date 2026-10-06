// <copyright file="FruitRulesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the stat-fruit basket rules: item levels, validity, per-component fees, buyout and bid ordering.
/// </summary>
[TestFixture]
public class FruitRulesTests
{
    /// <summary>
    /// Verifies that each fruit component maps to its Season 6 item level on the Fruits item.
    /// </summary>
    /// <param name="component">The fruit component.</param>
    /// <param name="itemLevel">The expected item level.</param>
    [TestCase(FruitComponent.Energy, 0)]
    [TestCase(FruitComponent.Vitality, 1)]
    [TestCase(FruitComponent.Agility, 2)]
    [TestCase(FruitComponent.Strength, 3)]
    [TestCase(FruitComponent.Command, 4)]
    public void ComponentMapsToItemLevel(FruitComponent component, int itemLevel)
    {
        Assert.That(FruitRules.GetItemLevel(component), Is.EqualTo(itemLevel));
    }

    /// <summary>
    /// Verifies that a starting basket needs every component within range and at least one component of two or more.
    /// </summary>
    /// <param name="strength">The Strength quantity.</param>
    /// <param name="agility">The Agility quantity.</param>
    /// <param name="vitality">The Vitality quantity.</param>
    /// <param name="energy">The Energy quantity.</param>
    /// <param name="command">The Command quantity.</param>
    /// <param name="isValid">Whether the basket is valid.</param>
    [TestCase(10, 0, 0, 0, 0, true)]
    [TestCase(1, 2, 0, 0, 0, true)]
    [TestCase(1, 0, 0, 0, 0, false)]
    [TestCase(0, 0, 0, 0, 0, false)]
    [TestCase(1, 1, 1, 1, 1, false)]
    [TestCase(10001, 0, 0, 0, 0, false)]
    [TestCase(10000, 10000, 10000, 10000, 10000, true)]
    public void StartingBasketIsValidOnlyWithinRangeAndWithAComponentOfTwoOrMore(
        long strength, long agility, long vitality, long energy, long command, bool isValid)
    {
        var basket = new FruitBasket(strength, agility, vitality, energy, command);

        Assert.That(FruitRules.IsValidStartingBasket(basket), Is.EqualTo(isValid));
    }

    /// <summary>
    /// Verifies that each nonzero component is charged its own rounded-up fee, and zero components are not charged.
    /// </summary>
    [Test]
    public void FeesAreCalculatedPerNonzeroComponent()
    {
        var basket = new FruitBasket(10, 5, 0, 20, 2);

        var fees = FruitRules.CalculateFees(basket, 100);

        Assert.That(fees, Is.EqualTo(new FruitBasket(1, 1, 0, 1, 1)));
    }

    /// <summary>
    /// Verifies a buyout must use the same nonzero components as the starting basket and be at least as large in each.
    /// </summary>
    /// <param name="buyout">The buyout basket.</param>
    /// <param name="isValid">Whether the buyout is valid.</param>
    [TestCase(10, 5, 0, 0, 0, true)]
    [TestCase(11, 5, 0, 0, 0, true)]
    [TestCase(9, 5, 0, 0, 0, false)]
    [TestCase(10, 4, 0, 0, 0, false)]
    [TestCase(10, 5, 0, 1, 0, false)]
    [TestCase(10, 0, 0, 0, 0, false)]
    public void BuyoutMustUseSameComponentsAndBeAtLeastStarting(long strength, long agility, long vitality, long energy, long command, bool isValid)
    {
        var starting = new FruitBasket(10, 5, 0, 0, 0);
        var buyout = new FruitBasket(strength, agility, vitality, energy, command);

        Assert.That(FruitRules.IsValidBuyout(starting, buyout), Is.EqualTo(isValid));
    }

    /// <summary>
    /// Verifies the first bid must meet every starting component, and cannot add a new component.
    /// </summary>
    /// <param name="bid">The bid basket.</param>
    /// <param name="isValid">Whether the bid is allowed.</param>
    [TestCase(10, 5, 0, 0, 0, true)]
    [TestCase(12, 5, 0, 0, 0, true)]
    [TestCase(9, 5, 0, 0, 0, false)]
    [TestCase(10, 4, 0, 0, 0, false)]
    [TestCase(10, 5, 0, 1, 0, false)]
    public void FirstBidMeetsEveryStartingComponentWithoutNewComponents(long strength, long agility, long vitality, long energy, long command, bool isValid)
    {
        var starting = new FruitBasket(10, 5, 0, 0, 0);
        var bid = new FruitBasket(strength, agility, vitality, energy, command);

        Assert.That(FruitRules.IsBidAllowed(starting, null, bid), Is.EqualTo(isValid));
    }

    /// <summary>
    /// Verifies a later bid must raise every nonzero component by the approved per-component increment.
    /// </summary>
    /// <param name="bid">The bid basket.</param>
    /// <param name="isValid">Whether the bid is allowed.</param>
    [TestCase(105, 42, 0, 0, 0, true)]
    [TestCase(106, 43, 0, 0, 0, true)]
    [TestCase(105, 41, 0, 0, 0, false)]
    [TestCase(104, 42, 0, 0, 0, false)]
    [TestCase(105, 42, 0, 1, 0, false)]
    public void LaterBidRaisesEveryNonzeroComponentByItsIncrement(long strength, long agility, long vitality, long energy, long command, bool isValid)
    {
        var starting = new FruitBasket(10, 5, 0, 0, 0);
        var current = new FruitBasket(100, 40, 0, 0, 0);
        var bid = new FruitBasket(strength, agility, vitality, energy, command);

        Assert.That(FruitRules.IsBidAllowed(starting, current, bid), Is.EqualTo(isValid));
    }

    /// <summary>
    /// Verifies the per-component minimum increment is at least one unit, even for small components.
    /// </summary>
    [Test]
    public void SmallComponentIncrementIsAtLeastOneUnit()
    {
        var starting = new FruitBasket(10, 0, 0, 0, 0);
        var current = new FruitBasket(10, 0, 0, 0, 0);

        Assert.That(FruitRules.IsBidAllowed(starting, current, new FruitBasket(11, 0, 0, 0, 0)), Is.True);
        Assert.That(FruitRules.IsBidAllowed(starting, current, new FruitBasket(10, 0, 0, 0, 0)), Is.False);
    }
}
