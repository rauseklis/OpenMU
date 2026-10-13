// <copyright file="AuctionSearchTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Verifies the browse query against PostgreSQL: only active listings of the requested currency, optional category filter,
/// stable sort orders, and paging eight to a page.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionSearchTests
{
    private Guid _sellerAccountId;

    [OneTimeSetUp]
    public void SetUp()
    {
        if (AuctionPostgresTestEnvironment.ConnectionString is null)
        {
            Assert.Ignore($"Set {AuctionPostgresTestEnvironment.ConnectionStringVariable} to run the PostgreSQL integration tests.");
        }

        AuctionPostgresTestEnvironment.EnsureConfigured();
    }

    [SetUp]
    public void Arrange()
    {
        this._sellerAccountId = Guid.NewGuid();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._sellerAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.SellerAccountId == this._sellerAccountId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies only active listings of the requested currency are returned, and the category filter narrows them.
    /// </summary>
    [Test]
    public async Task ReturnsOnlyActiveListingsOfTheCurrencyAndCategory()
    {
        await this.List(currency: 0, category: 0, startingPrice: 1000);
        await this.List(currency: 0, category: 1, startingPrice: 1000);
        await this.List(currency: 1, category: 4, startingPrice: 2);

        var zen = await this.Search(new AuctionListingSearch(Currency: 0));
        var armor = await this.Search(new AuctionListingSearch(Currency: 0, Category: 1));

        Assert.Multiple(() =>
        {
            Assert.That(zen.TotalCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(zen.Rows.All(r => r.Currency == 0), Is.True);
            Assert.That(armor.Rows.Count(r => r.SellerAccountId == this._sellerAccountId), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies a sub-category narrows the category to exactly the matching listings.
    /// </summary>
    [Test]
    public async Task SubCategoryNarrowsTheCategory()
    {
        await this.List(currency: 0, category: 1, startingPrice: 10, subCategory: 3);
        await this.List(currency: 0, category: 1, startingPrice: 20, subCategory: 4);
        await this.List(currency: 0, category: 0, startingPrice: 30, subCategory: 3);

        var gloves = await this.Search(new AuctionListingSearch(Currency: 0, Category: 1, SubCategory: 3, PageSize: 1000));

        Assert.That(gloves.Rows.Count(r => r.SellerAccountId == this._sellerAccountId), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies a sub-category the category does not have is an empty page, not an error.
    /// </summary>
    [Test]
    public async Task UnknownSubCategoryReturnsNoRows()
    {
        await this.List(currency: 0, category: 1, startingPrice: 10, subCategory: 3);

        var page = await this.Search(new AuctionListingSearch(Currency: 0, Category: 1, SubCategory: 99, PageSize: 1000));

        Assert.That(page.Rows.Count(r => r.SellerAccountId == this._sellerAccountId), Is.Zero);
    }

    /// <summary>
    /// Verifies the marketplace view can return matching listings priced in different currencies. Mixed-currency
    /// rows are intentionally only ordered by a currency-neutral field such as expiry.
    /// </summary>
    [Test]
    public async Task ReturnsAllCurrenciesWhenCurrencyIsNotFiltered()
    {
        var zen = await this.List(currency: 0, category: 0, startingPrice: 1000);
        var chaos = await this.List(currency: 1, category: 0, startingPrice: 2);

        var page = await this.Search(new AuctionListingSearch(Currency: null, PageSize: 1000));
        var listingIds = page.Rows.Where(row => row.SellerAccountId == this._sellerAccountId).Select(row => row.ListingId);

        Assert.That(listingIds, Is.EquivalentTo(new[] { zen, chaos }));
    }

    /// <summary>
    /// Verifies a cancelled listing is no longer returned.
    /// </summary>
    [Test]
    public async Task CancelledListingIsNotReturned()
    {
        var listingId = await this.List(currency: 0, category: 0, startingPrice: 1000);
        await new AuctionHouseRepository().CancelAsync(new EntityDataContext(), new CancelDraft(
            OperationId: Guid.NewGuid(),
            AccountId: this._sellerAccountId,
            ListingId: listingId,
            ExpectedVersion: 0,
            Now: DateTimeOffset.UtcNow));

        var page = await this.Search(new AuctionListingSearch(Currency: 0, Category: 0));

        Assert.That(page.Rows.Any(r => r.ListingId == listingId), Is.False);
    }

    /// <summary>
    /// Verifies the ending-soonest order lists the earliest expiry first.
    /// </summary>
    [Test]
    public async Task EndingSoonestOrdersByExpiry()
    {
        var later = await this.List(currency: 1, category: 4, startingPrice: 2, hours: 48);
        var sooner = await this.List(currency: 1, category: 4, startingPrice: 2, hours: 6);

        var page = await this.Search(new AuctionListingSearch(Currency: 1, Sort: AuctionSearchSort.EndingSoonest));

        var rows = page.Rows.Where(r => r.ListingId == later || r.ListingId == sooner).Select(r => r.ListingId).ToList();
        Assert.That(rows, Is.EqualTo(new[] { sooner, later }));
    }

    /// <summary>
    /// Verifies a page holds eight listings and the total count covers every match.
    /// </summary>
    [Test]
    public async Task PagesHoldEightListings()
    {
        for (var i = 0; i < 9; i++)
        {
            await this.List(currency: 1, category: 4, startingPrice: 2);
        }

        var first = await this.Search(new AuctionListingSearch(Currency: 1, Category: 4, Page: 1));
        var second = await this.Search(new AuctionListingSearch(Currency: 1, Category: 4, Page: 2));

        Assert.Multiple(() =>
        {
            Assert.That(first.Rows.Count, Is.EqualTo(8));
            Assert.That(first.TotalCount, Is.EqualTo(second.TotalCount));
            Assert.That(second.Rows.Count, Is.EqualTo(first.TotalCount - 8));
        });
    }

    /// <summary>
    /// Verifies a scalar price sort is ascending by the starting price when there are no bids.
    /// </summary>
    [Test]
    public async Task PriceSortUsesTheEffectivePrice()
    {
        var expensive = await this.List(currency: 0, category: 0, startingPrice: 3000);
        var cheap = await this.List(currency: 0, category: 0, startingPrice: 1000);

        var page = await this.Search(new AuctionListingSearch(Currency: 0, Category: 0, Sort: AuctionSearchSort.PriceAscending, PageSize: 1000));

        var rows = page.Rows.Where(r => r.ListingId == expensive || r.ListingId == cheap).Select(r => r.ListingId).ToList();
        Assert.That(rows, Is.EqualTo(new[] { cheap, expensive }));
    }

    /// <summary>
    /// Verifies the item-name, level, scalar-price, and remaining-time filters use the immutable listing snapshots.
    /// </summary>
    [Test]
    public async Task AppliesSnapshotAndScalarRangeFilters()
    {
        var breaker = await this.List(currency: 0, category: 0, startingPrice: 1000, hours: 6);
        var darkSteel = await this.List(currency: 0, category: 0, startingPrice: 3000, hours: 48);
        var lightSaber = await this.List(currency: 0, category: 0, startingPrice: 2000, hours: 12);
        await using (var context = new EntityDataContext())
        {
            await context.Set<AuctionListing>().Where(l => l.Id == breaker).ExecuteUpdateAsync(s => s
                .SetProperty(l => l.NormalizedName, "DARK BREAKER")
                .SetProperty(l => l.ItemLevel, 9));
            await context.Set<AuctionListing>().Where(l => l.Id == darkSteel).ExecuteUpdateAsync(s => s
                .SetProperty(l => l.NormalizedName, "DARK STEEL")
                .SetProperty(l => l.ItemLevel, 12));
            await context.Set<AuctionListing>().Where(l => l.Id == lightSaber).ExecuteUpdateAsync(s => s
                .SetProperty(l => l.NormalizedName, "LIGHT SABER")
                .SetProperty(l => l.ItemLevel, 7));
        }

        var byName = await this.Search(new AuctionListingSearch(Currency: 0, Name: "dark", PageSize: 1000));
        var byLevel = await this.Search(new AuctionListingSearch(Currency: 0, LevelMinimum: 8, LevelMaximum: 10, PageSize: 1000));
        var byPrice = await this.Search(new AuctionListingSearch(Currency: 0, MinimumScalar: 1500, MaximumScalar: 2500, PageSize: 1000));
        var byRemainingTime = await this.Search(new AuctionListingSearch(Currency: 0, MaxRemainingHours: 8, PageSize: 1000));

        Assert.Multiple(() =>
        {
            Assert.That(byName.Rows.Where(r => r.SellerAccountId == this._sellerAccountId).Select(r => r.ListingId), Is.EquivalentTo(new[] { breaker, darkSteel }));
            Assert.That(byLevel.Rows.Where(r => r.SellerAccountId == this._sellerAccountId).Select(r => r.ListingId), Is.EqualTo(new[] { breaker }));
            Assert.That(byPrice.Rows.Where(r => r.SellerAccountId == this._sellerAccountId).Select(r => r.ListingId), Is.EqualTo(new[] { lightSaber }));
            Assert.That(byRemainingTime.Rows.Where(r => r.SellerAccountId == this._sellerAccountId).Select(r => r.ListingId), Is.EqualTo(new[] { breaker }));
        });
    }

    /// <summary>
    /// Verifies class and option filters use their immutable bit-mask snapshots and require every selected option.
    /// </summary>
    [Test]
    public async Task AppliesClassAndOptionFlagFilters()
    {
        var wizardLuckSkill = await this.List(currency: 0, category: 0, startingPrice: 1000);
        var knightLuck = await this.List(currency: 0, category: 0, startingPrice: 2000);
        await using (var context = new EntityDataContext())
        {
            await context.Set<AuctionListing>().Where(l => l.Id == wizardLuckSkill).ExecuteUpdateAsync(s => s
                .SetProperty(l => l.ClassMask, 1L)
                .SetProperty(l => l.OptionFlags, (int)(AuctionItemOptionFlags.HasLuck | AuctionItemOptionFlags.HasSkill)));
            await context.Set<AuctionListing>().Where(l => l.Id == knightLuck).ExecuteUpdateAsync(s => s
                .SetProperty(l => l.ClassMask, 2L)
                .SetProperty(l => l.OptionFlags, (int)AuctionItemOptionFlags.HasLuck));
        }

        var byClass = await this.Search(new AuctionListingSearch(Currency: 0, ClassMask: 1, PageSize: 1000));
        var byOptions = await this.Search(new AuctionListingSearch(
            Currency: 0,
            OptionFlags: AuctionItemOptionFlags.HasLuck | AuctionItemOptionFlags.HasSkill,
            PageSize: 1000));

        Assert.Multiple(() =>
        {
            Assert.That(byClass.Rows.Where(r => r.SellerAccountId == this._sellerAccountId).Select(r => r.ListingId), Is.EqualTo(new[] { wizardLuckSkill }));
            Assert.That(byOptions.Rows.Where(r => r.SellerAccountId == this._sellerAccountId).Select(r => r.ListingId), Is.EqualTo(new[] { wizardLuckSkill }));
        });
    }

    /// <summary>
    /// Verifies a price sort is refused for fruit baskets, because baskets have no defensible total order.
    /// </summary>
    [Test]
    public void PriceSortIsRefusedForFruitBaskets()
    {
        Assert.That(
            async () => await this.Search(new AuctionListingSearch(Currency: 8, Sort: AuctionSearchSort.PriceAscending)),
            Throws.TypeOf<ArgumentException>());
    }

    /// <summary>
    /// A price order across Zen, jewels and fruit would imply an exchange rate the game does not define.
    /// </summary>
    [Test]
    public void PriceSortIsRefusedWhenCurrencyIsNotFiltered()
    {
        Assert.That(
            async () => await this.Search(new AuctionListingSearch(Currency: null, Sort: AuctionSearchSort.PriceAscending)),
            Throws.TypeOf<ArgumentException>());
    }

    private async Task<Guid> List(byte currency, byte category, long startingPrice, int hours = 24, byte subCategory = 255)
    {
        var now = DateTimeOffset.UtcNow;
        var created = await new AuctionHouseRepository().CreateListingAsync(new EntityDataContext(), new AuctionListingDraft(
            OperationId: Guid.NewGuid(),
            AccountId: this._sellerAccountId,
            ItemId: Guid.NewGuid(),
            SellerCharacterId: Guid.NewGuid(),
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            Currency: currency,
            Category: category,
            StartingPrice: startingPrice,
            CreatedAt: now,
            EndsAt: now.AddHours(hours),
            SubCategory: subCategory));
        return created.ListingId;
    }

    private Task<AuctionListingPage> Search(AuctionListingSearch search)
    {
        return new AuctionHouseRepository().SearchListingsAsync(new EntityDataContext(), search);
    }
}
