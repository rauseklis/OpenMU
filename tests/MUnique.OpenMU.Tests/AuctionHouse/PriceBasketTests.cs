// <copyright file="PriceBasketTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the price basket (a quantity per price kind) and the bid rule for mixed-currency listings: a bid keeps every
/// currency of the current bid at the same amount or higher and raises at least one.
/// </summary>
[TestFixture]
public class PriceBasketTests
{
    /// <summary>
    /// Verifies a basket survives a round trip through its text form, which is how it is stored.
    /// </summary>
    [Test]
    public void TextFormRoundTrips()
    {
        var basket = PriceBasket.FromComponents(new[] { Part(PriceKind.Zen, 250_000), Part(PriceKind.Soul, 3), Part(PriceKind.CommandFruit, 12) });

        var text = basket.ToVectorText();

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("250000,0,0,3,0,0,0,0,0,0,0,0,12"));
            Assert.That(PriceBasket.Parse(text), Is.EqualTo(basket));
        });
    }

    /// <summary>
    /// Verifies malformed stored text is refused instead of read as a partial price.
    /// </summary>
    /// <param name="text">The stored text.</param>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("1,2,3")]
    [TestCase("1,2,3,4,5,6,7,8,9,10,11,12,13,14")]
    [TestCase("1,2,3,4,5,6,7,8,9,10,11,12,x")]
    [TestCase("1,2,3,4,5,6,7,8,9,10,11,12,-1")]
    public void MalformedTextIsNotABasket(string? text)
    {
        Assert.That(PriceBasket.Parse(text), Is.Null);
    }

    /// <summary>
    /// Verifies the same kind twice is refused when building a basket from components.
    /// </summary>
    [Test]
    public void SameKindTwiceIsRefused()
    {
        Assert.That(
            () => PriceBasket.FromComponents(new[] { Part(PriceKind.Soul, 1), Part(PriceKind.Soul, 2) }),
            Throws.ArgumentException);
    }

    /// <summary>
    /// Verifies the components come back in kind order, without the empty ones, and the kind mask has one bit per kind.
    /// </summary>
    [Test]
    public void ComponentsAndKindMaskListOnlyThePricedKinds()
    {
        var basket = PriceBasket.FromComponents(new[] { Part(PriceKind.CommandFruit, 2), Part(PriceKind.Zen, 5000), Part(PriceKind.Soul, 1) });

        Assert.Multiple(() =>
        {
            Assert.That(basket.Components.Select(c => c.Kind), Is.EqualTo(new[] { PriceKind.Zen, PriceKind.Soul, PriceKind.CommandFruit }));
            Assert.That(basket.KindMask, Is.EqualTo((1 << 0) | (1 << 3) | (1 << 12)));
            Assert.That(PriceBasket.Empty.IsEmpty, Is.True);
            Assert.That(basket.IsEmpty, Is.False);
        });
    }

    /// <summary>
    /// Verifies dominance: every currency at least as high; a raise also needs one strictly higher.
    /// </summary>
    [Test]
    public void DominanceAndRaiseFollowTheOwnerRule()
    {
        var current = PriceBasket.FromComponents(new[] { Part(PriceKind.Soul, 3), Part(PriceKind.Bless, 2) });

        Assert.Multiple(() =>
        {
            Assert.That(Basket((PriceKind.Soul, 3), (PriceKind.Bless, 3)).IsRaiseOf(current), Is.True);
            Assert.That(Basket((PriceKind.Soul, 4), (PriceKind.Bless, 2)).IsRaiseOf(current), Is.True);
            Assert.That(Basket((PriceKind.Soul, 3), (PriceKind.Bless, 2), (PriceKind.Chaos, 1)).IsRaiseOf(current), Is.True, "adding a currency raises");
            Assert.That(Basket((PriceKind.Soul, 3), (PriceKind.Bless, 2)).IsRaiseOf(current), Is.False, "equal is not a raise");
            Assert.That(Basket((PriceKind.Soul, 9), (PriceKind.Bless, 1)).IsRaiseOf(current), Is.False, "less of any currency is never a raise");
            Assert.That(Basket((PriceKind.Soul, 9)).IsRaiseOf(current), Is.False, "dropping a currency is never a raise");
            Assert.That(Basket((PriceKind.Soul, 3), (PriceKind.Bless, 2)).Dominates(current), Is.True);
        });
    }

    /// <summary>
    /// Verifies subtraction never goes below zero (a buyer's own escrow is subtracted from the buyout).
    /// </summary>
    [Test]
    public void MinusClampsAtZero()
    {
        var result = Basket((PriceKind.Soul, 5), (PriceKind.Bless, 1)).Minus(Basket((PriceKind.Soul, 2), (PriceKind.Bless, 4), (PriceKind.Chaos, 1)));

        Assert.That(result, Is.EqualTo(Basket((PriceKind.Soul, 3))));
    }

    /// <summary>
    /// Verifies the first bid must match the starting price in every currency, and may add currencies.
    /// </summary>
    [Test]
    public void FirstBidMustCoverTheStartingPrice()
    {
        var starting = Basket((PriceKind.Soul, 3), (PriceKind.Bless, 2));

        Assert.Multiple(() =>
        {
            Assert.That(PriceRules.IsBidAllowed(starting, null, Basket((PriceKind.Soul, 3), (PriceKind.Bless, 2))), Is.True);
            Assert.That(PriceRules.IsBidAllowed(starting, null, Basket((PriceKind.Soul, 3), (PriceKind.Bless, 2), (PriceKind.Chaos, 1))), Is.True);
            Assert.That(PriceRules.IsBidAllowed(starting, null, Basket((PriceKind.Soul, 3), (PriceKind.Bless, 1))), Is.False);
            Assert.That(PriceRules.IsBidAllowed(starting, null, Basket((PriceKind.Soul, 9))), Is.False);
            Assert.That(PriceRules.IsBidAllowed(starting, null, PriceBasket.Empty), Is.False);
        });
    }

    /// <summary>
    /// Verifies later bids must raise the current bid, and that a bid is validated like a price.
    /// </summary>
    [Test]
    public void LaterBidsMustRaiseTheCurrentBidAndStayWithinLimits()
    {
        var starting = Basket((PriceKind.Soul, 3));
        var current = Basket((PriceKind.Soul, 4), (PriceKind.Bless, 1));

        Assert.Multiple(() =>
        {
            Assert.That(PriceRules.IsBidAllowed(starting, current, Basket((PriceKind.Soul, 4), (PriceKind.Bless, 2))), Is.True);
            Assert.That(PriceRules.IsBidAllowed(starting, current, Basket((PriceKind.Soul, 4), (PriceKind.Bless, 1))), Is.False);
            Assert.That(PriceRules.IsBidAllowed(starting, current, Basket((PriceKind.Soul, 5))), Is.False, "must keep Bless");
            Assert.That(
                PriceRules.IsBidAllowed(starting, current, Basket(
                    (PriceKind.Soul, 5), (PriceKind.Bless, 1), (PriceKind.Chaos, 1), (PriceKind.Life, 1), (PriceKind.Creation, 1), (PriceKind.Guardian, 1))),
                Is.False,
                "six currencies is one too many");
            Assert.That(
                PriceRules.IsBidAllowed(Basket((PriceKind.Zen, 5000)), null, Basket((PriceKind.Zen, 2_000_000_001))),
                Is.False,
                "quantity above the Zen limit");
        });
    }

    private static PriceComponent Part(PriceKind kind, long quantity) => new(kind, quantity);

    private static PriceBasket Basket(params (PriceKind Kind, long Quantity)[] parts)
        => PriceBasket.FromComponents(parts.Select(p => new PriceComponent(p.Kind, p.Quantity)));
}
