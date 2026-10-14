// <copyright file="PriceRulesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the mixed-price rules: a listing's price is a list of up to five components drawn from Zen, the jewels
/// and the stat fruits, and any listing with a starting price can take bids.
/// </summary>
[TestFixture]
public class PriceRulesTests
{
    private static readonly PriceComponent[] None = Array.Empty<PriceComponent>();

    /// <summary>
    /// Verifies that a price of several jewel kinds is a valid buy-now price.
    /// </summary>
    [Test]
    public void BuyNowPriceOfSeveralJewelsIsValid()
    {
        var buyout = new[] { Part(PriceKind.Bless, 4), Part(PriceKind.Soul, 2), Part(PriceKind.Chaos, 10) };

        Assert.That(PriceRules.ValidateListing(None, buyout), Is.EqualTo(PriceValidationResult.Valid));
    }

    /// <summary>
    /// Verifies that Zen, jewels and fruits can be mixed in one price.
    /// </summary>
    [Test]
    public void BuyNowPriceMixingZenJewelsAndFruitsIsValid()
    {
        var buyout = new[]
        {
            Part(PriceKind.Life, 103), Part(PriceKind.StrengthFruit, 500), Part(PriceKind.Creation, 11), Part(PriceKind.Zen, 500_000),
        };

        Assert.That(PriceRules.ValidateListing(None, buyout), Is.EqualTo(PriceValidationResult.Valid));
    }

    /// <summary>
    /// Verifies that a listing needs some price.
    /// </summary>
    [Test]
    public void ListingWithoutAnyPriceIsRejected()
    {
        Assert.That(PriceRules.ValidateListing(None, None), Is.EqualTo(PriceValidationResult.NoPrice));
    }

    /// <summary>
    /// Verifies that five components are the most one price can hold.
    /// </summary>
    [Test]
    public void FivePriceComponentsAreAllowedAndSixAreNot()
    {
        var five = new[]
        {
            Part(PriceKind.Zen, 5000), Part(PriceKind.Chaos, 1), Part(PriceKind.Bless, 1), Part(PriceKind.Soul, 1), Part(PriceKind.Life, 1),
        };
        var six = five.Append(Part(PriceKind.Creation, 1)).ToArray();

        Assert.That(PriceRules.ValidateListing(None, five), Is.EqualTo(PriceValidationResult.Valid));
        Assert.That(PriceRules.ValidateListing(None, six), Is.EqualTo(PriceValidationResult.TooManyComponents));
    }

    /// <summary>
    /// Verifies that the component limit is configurable.
    /// </summary>
    [Test]
    public void ComponentLimitFollowsTheConfiguredLimits()
    {
        var three = new[] { Part(PriceKind.Chaos, 1), Part(PriceKind.Bless, 1), Part(PriceKind.Soul, 1) };
        var limits = new PriceLimits { MaximumComponents = 2 };

        Assert.That(PriceRules.ValidateListing(None, three, limits), Is.EqualTo(PriceValidationResult.TooManyComponents));
        Assert.That(PriceRules.ValidateListing(None, three), Is.EqualTo(PriceValidationResult.Valid));
    }

    /// <summary>
    /// Verifies that a kind may appear only once in a price.
    /// </summary>
    [Test]
    public void SameKindTwiceIsRejected()
    {
        var buyout = new[] { Part(PriceKind.Bless, 1), Part(PriceKind.Bless, 2) };

        Assert.That(PriceRules.ValidateListing(None, buyout), Is.EqualTo(PriceValidationResult.DuplicateComponent));
    }

    /// <summary>
    /// Verifies the default quantity range of every kind of component.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="quantity">The quantity.</param>
    /// <param name="isValid">Whether the quantity is in range.</param>
    [TestCase(PriceKind.Zen, 999, false)]
    [TestCase(PriceKind.Zen, 1000, true)]
    [TestCase(PriceKind.Zen, 2_000_000_000, true)]
    [TestCase(PriceKind.Zen, 2_000_000_001, false)]
    [TestCase(PriceKind.Zen, -5, false)]
    [TestCase(PriceKind.Bless, 0, false)]
    [TestCase(PriceKind.Bless, 1, true)]
    [TestCase(PriceKind.Bless, 10_000, true)]
    [TestCase(PriceKind.Bless, 10_001, false)]
    [TestCase(PriceKind.StrengthFruit, 1, true)]
    [TestCase(PriceKind.CommandFruit, 10_000, true)]
    [TestCase(PriceKind.CommandFruit, 10_001, false)]
    public void QuantityMustBeWithinTheRangeOfItsKind(PriceKind kind, long quantity, bool isValid)
    {
        var expected = isValid ? PriceValidationResult.Valid : PriceValidationResult.QuantityOutOfRange;

        Assert.That(PriceRules.ValidateListing(None, new[] { Part(kind, quantity) }), Is.EqualTo(expected));
    }

    /// <summary>
    /// Verifies that the quantity ranges are configurable.
    /// </summary>
    [Test]
    public void QuantityRangeFollowsTheConfiguredLimits()
    {
        var limits = new PriceLimits { ZenMinimum = 5000, JewelMaximum = 50 };

        Assert.That(PriceRules.ValidateListing(None, new[] { Part(PriceKind.Zen, 4999) }, limits), Is.EqualTo(PriceValidationResult.QuantityOutOfRange));
        Assert.That(PriceRules.ValidateListing(None, new[] { Part(PriceKind.Zen, 5000) }, limits), Is.EqualTo(PriceValidationResult.Valid));
        Assert.That(PriceRules.ValidateListing(None, new[] { Part(PriceKind.Soul, 51) }, limits), Is.EqualTo(PriceValidationResult.QuantityOutOfRange));
    }

    /// <summary>
    /// Verifies that an auction's starting price may mix several currencies.
    /// </summary>
    [Test]
    public void AuctionStartingPriceMayMixSeveralCurrencies()
    {
        var starting = new[] { Part(PriceKind.Bless, 5), Part(PriceKind.Soul, 5) };

        Assert.That(PriceRules.ValidateListing(starting, None), Is.EqualTo(PriceValidationResult.Valid));
    }

    /// <summary>
    /// Verifies that an auction may have no buyout, or one at least as high in the same kind.
    /// </summary>
    [Test]
    public void AuctionBuyoutIsOptionalAndAtLeastTheStartingPriceInTheSameKind()
    {
        var starting = new[] { Part(PriceKind.Bless, 5) };

        Assert.That(PriceRules.ValidateListing(starting, None), Is.EqualTo(PriceValidationResult.Valid));
        Assert.That(PriceRules.ValidateListing(starting, new[] { Part(PriceKind.Bless, 5) }), Is.EqualTo(PriceValidationResult.Valid));
        Assert.That(PriceRules.ValidateListing(starting, new[] { Part(PriceKind.Bless, 9) }), Is.EqualTo(PriceValidationResult.Valid));
        Assert.That(PriceRules.ValidateListing(starting, new[] { Part(PriceKind.Bless, 4) }), Is.EqualTo(PriceValidationResult.BuyoutBelowStart));
    }

    /// <summary>
    /// Verifies that an auction's buyout cannot change or add a kind.
    /// </summary>
    [Test]
    public void AuctionBuyoutMustUseExactlyTheStartingKind()
    {
        var starting = new[] { Part(PriceKind.Bless, 5) };

        Assert.That(PriceRules.ValidateListing(starting, new[] { Part(PriceKind.Soul, 9) }), Is.EqualTo(PriceValidationResult.BuyoutKindsDiffer));
        Assert.That(
            PriceRules.ValidateListing(starting, new[] { Part(PriceKind.Bless, 9), Part(PriceKind.Soul, 1) }),
            Is.EqualTo(PriceValidationResult.BuyoutKindsDiffer));
    }

    /// <summary>
    /// Verifies that a mixed auction's buyout covers exactly the starting currencies, each at least as high.
    /// </summary>
    [Test]
    public void MixedAuctionBuyoutCoversEveryStartingCurrency()
    {
        var starting = new[] { Part(PriceKind.Bless, 5), Part(PriceKind.Soul, 2) };

        Assert.Multiple(() =>
        {
            Assert.That(PriceRules.ValidateListing(starting, new[] { Part(PriceKind.Soul, 2), Part(PriceKind.Bless, 5) }), Is.EqualTo(PriceValidationResult.Valid));
            Assert.That(PriceRules.ValidateListing(starting, new[] { Part(PriceKind.Bless, 9), Part(PriceKind.Soul, 4) }), Is.EqualTo(PriceValidationResult.Valid));
            Assert.That(PriceRules.ValidateListing(starting, new[] { Part(PriceKind.Bless, 9) }), Is.EqualTo(PriceValidationResult.BuyoutKindsDiffer));
            Assert.That(PriceRules.ValidateListing(starting, new[] { Part(PriceKind.Bless, 4), Part(PriceKind.Soul, 2) }), Is.EqualTo(PriceValidationResult.BuyoutBelowStart));
        });
    }

    /// <summary>
    /// Verifies that the quantities of the starting price are range-checked too.
    /// </summary>
    [Test]
    public void AuctionStartingPriceIsRangeChecked()
    {
        Assert.That(
            PriceRules.ValidateListing(new[] { Part(PriceKind.Zen, 10) }, None),
            Is.EqualTo(PriceValidationResult.QuantityOutOfRange));
    }

    /// <summary>
    /// Verifies that any listing with a starting price takes bids, and a buy-now listing (no starting price) does not.
    /// </summary>
    [Test]
    public void AnyStartingPriceIsBiddable()
    {
        Assert.That(PriceRules.IsBiddable(new[] { Part(PriceKind.Chaos, 3) }), Is.True);
        Assert.That(PriceRules.IsBiddable(None), Is.False);
        Assert.That(PriceRules.IsBiddable(new[] { Part(PriceKind.Chaos, 3), Part(PriceKind.Bless, 1) }), Is.True);
    }

    /// <summary>
    /// Verifies that a scalar price becomes one component in its own currency.
    /// </summary>
    [Test]
    public void ScalarPriceBecomesOneComponent()
    {
        Assert.That(PriceRules.FromScalar(AuctionCurrencyMode.Bless, 4), Is.EqualTo(new[] { Part(PriceKind.Bless, 4) }));
        Assert.That(PriceRules.FromScalar(AuctionCurrencyMode.Zen, 137_500), Is.EqualTo(new[] { Part(PriceKind.Zen, 137_500) }));
        Assert.That(PriceRules.FromScalar(AuctionCurrencyMode.Harmony, 0), Is.Empty);
        Assert.That(() => PriceRules.FromScalar(AuctionCurrencyMode.Fruits, 4), Throws.ArgumentException);
    }

    /// <summary>
    /// Verifies that a fruit basket becomes one component per nonzero fruit, in fruit order.
    /// </summary>
    [Test]
    public void FruitBasketBecomesOneComponentPerNonzeroFruit()
    {
        var components = PriceRules.FromFruitBasket(new FruitBasket(500, 0, 0, 12, 3));

        Assert.That(
            components,
            Is.EqualTo(new[] { Part(PriceKind.StrengthFruit, 500), Part(PriceKind.EnergyFruit, 12), Part(PriceKind.CommandFruit, 3) }));
        Assert.That(PriceRules.FromFruitBasket(default), Is.Empty);
    }

    /// <summary>
    /// Verifies that the legacy mode and amounts convert by mode.
    /// </summary>
    [Test]
    public void LegacyPriceConvertsByMode()
    {
        var basket = new FruitBasket(7, 0, 0, 0, 0);

        Assert.That(PriceRules.FromLegacy(AuctionCurrencyMode.Fruits, 0, basket), Is.EqualTo(new[] { Part(PriceKind.StrengthFruit, 7) }));
        Assert.That(PriceRules.FromLegacy(AuctionCurrencyMode.Life, 9, default), Is.EqualTo(new[] { Part(PriceKind.Life, 9) }));
    }

    /// <summary>
    /// Verifies the kind classification.
    /// </summary>
    [Test]
    public void KindsAreClassifiedAsZenJewelOrFruit()
    {
        Assert.That(PriceRules.IsJewel(PriceKind.Chaos), Is.True);
        Assert.That(PriceRules.IsJewel(PriceKind.Harmony), Is.True);
        Assert.That(PriceRules.IsJewel(PriceKind.Zen), Is.False);
        Assert.That(PriceRules.IsJewel(PriceKind.CommandFruit), Is.False);
        Assert.That(PriceRules.IsFruit(PriceKind.StrengthFruit), Is.True);
        Assert.That(PriceRules.IsFruit(PriceKind.Soul), Is.False);
    }

    /// <summary>
    /// Verifies that the wire values of the kinds are stable.
    /// </summary>
    [Test]
    public void KindWireValuesAreStable()
    {
        Assert.That((int)PriceKind.Zen, Is.EqualTo(0));
        Assert.That((int)PriceKind.Harmony, Is.EqualTo(7));
        Assert.That((int)PriceKind.StrengthFruit, Is.EqualTo(8));
        Assert.That((int)PriceKind.CommandFruit, Is.EqualTo(12));
    }

    private static PriceComponent Part(PriceKind kind, long quantity) => new(kind, quantity);
}
