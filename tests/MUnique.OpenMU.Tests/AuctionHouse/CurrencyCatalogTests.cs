// <copyright file="CurrencyCatalogTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the canonical currency catalog and tender eligibility of inventory items.
/// </summary>
[TestFixture]
public class CurrencyCatalogTests
{
    /// <summary>
    /// Verifies each jewel mode maps to its canonical loose identity.
    /// </summary>
    /// <param name="mode">The currency mode.</param>
    /// <param name="group">The expected group.</param>
    /// <param name="number">The expected number.</param>
    [TestCase(AuctionCurrencyMode.Chaos, 12, 15)]
    [TestCase(AuctionCurrencyMode.Bless, 14, 13)]
    [TestCase(AuctionCurrencyMode.Soul, 14, 14)]
    [TestCase(AuctionCurrencyMode.Life, 14, 16)]
    [TestCase(AuctionCurrencyMode.Creation, 14, 22)]
    [TestCase(AuctionCurrencyMode.Guardian, 14, 31)]
    [TestCase(AuctionCurrencyMode.Harmony, 14, 42)]
    public void JewelModeMapsToCanonicalIdentity(AuctionCurrencyMode mode, int group, int number)
    {
        var identity = CurrencyCatalog.GetLooseIdentity(mode);

        Assert.That(identity, Is.EqualTo(new ItemIdentity(group, number, 0)));
    }

    /// <summary>
    /// Verifies Zen is money rather than an item, and the fruit basket has no single loose identity.
    /// </summary>
    [Test]
    public void ZenAndFruitsHaveNoSingleLooseItemIdentity()
    {
        Assert.That(CurrencyCatalog.GetLooseIdentity(AuctionCurrencyMode.Zen), Is.Null);
        Assert.That(CurrencyCatalog.GetLooseIdentity(AuctionCurrencyMode.Fruits), Is.Null);
    }

    /// <summary>
    /// Verifies a clean canonical loose jewel is tender for its own mode.
    /// </summary>
    [Test]
    public void CleanCanonicalJewelIsTenderForItsMode()
    {
        var chaos = new TenderCandidate(12, 15, 0, Durability: 1, HasOptions: false, IsBound: false);

        Assert.That(CurrencyCatalog.IsCanonicalTender(AuctionCurrencyMode.Chaos, chaos), Is.True);
        Assert.That(CurrencyCatalog.IsCanonicalTender(AuctionCurrencyMode.Bless, chaos), Is.False);
    }

    /// <summary>
    /// Verifies items that are not clean canonical tender are rejected for their mode.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="level">The item level.</param>
    /// <param name="durability">The durability.</param>
    /// <param name="hasOptions">Whether the item has excellent, ancient, harmony or socket options.</param>
    /// <param name="isBound">Whether the item is character-bound.</param>
    [TestCase(12, 15, 0, 0, false, false)]
    [TestCase(12, 15, 0, 1, true, false)]
    [TestCase(12, 15, 0, 1, false, true)]
    [TestCase(12, 16, 0, 1, false, false)]
    [TestCase(12, 15, 1, 1, false, false)]
    public void DirtyOrNonCanonicalJewelIsNotTender(int group, int number, int level, int durability, bool hasOptions, bool isBound)
    {
        var candidate = new TenderCandidate(group, number, level, durability, hasOptions, isBound);

        Assert.That(CurrencyCatalog.IsCanonicalTender(AuctionCurrencyMode.Chaos, candidate), Is.False);
    }

    /// <summary>
    /// Verifies a fruit at each mapped level is tender for the Fruits mode, and reset fruits are not.
    /// </summary>
    /// <param name="number">The item number.</param>
    /// <param name="level">The item level.</param>
    /// <param name="isTender">Whether the item is tender for the Fruits mode.</param>
    [TestCase(15, 0, true)]
    [TestCase(15, 4, true)]
    [TestCase(15, 5, false)]
    [TestCase(54, 0, false)]
    [TestCase(58, 0, false)]
    public void FruitsAreTenderOnlyAtMappedLevels(int number, int level, bool isTender)
    {
        var candidate = new TenderCandidate(13, number, level, Durability: 1, HasOptions: false, IsBound: false);

        Assert.That(CurrencyCatalog.IsCanonicalTender(AuctionCurrencyMode.Fruits, candidate), Is.EqualTo(isTender));
    }

    /// <summary>
    /// Verifies Zen is never satisfied by an item.
    /// </summary>
    [Test]
    public void ZenIsNeverSatisfiedByAnItem()
    {
        var candidate = new TenderCandidate(12, 15, 0, Durability: 1, HasOptions: false, IsBound: false);

        Assert.That(CurrencyCatalog.IsCanonicalTender(AuctionCurrencyMode.Zen, candidate), Is.False);
    }
}
