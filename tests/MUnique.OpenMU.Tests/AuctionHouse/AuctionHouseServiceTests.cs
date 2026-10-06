// <copyright file="AuctionHouseServiceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies the read-only service operations against PostgreSQL: a disabled feature refuses every request, browsing is
/// bounded to the configured page size, an invalid sort is reported as a request error, detail reports a missing listing,
/// and open reports the pending Mailbox count.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionHouseServiceTests
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
        await context.Set<AuctionCollection>().Where(c => c.AccountId == this._sellerAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._sellerAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.SellerAccountId == this._sellerAccountId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies a disabled feature refuses browsing without querying.
    /// </summary>
    [Test]
    public async Task DisabledFeatureRefusesBrowse()
    {
        var service = this.CreateService(enabled: false);

        var result = await service.BrowseAsync(new AuctionListingSearch(Currency: 0));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Disabled));
    }

    /// <summary>
    /// Verifies a browse returns at most the configured page size.
    /// </summary>
    [Test]
    public async Task BrowseIsBoundedToTheConfiguredPageSize()
    {
        for (var i = 0; i < 9; i++)
        {
            await this.List(startingPrice: 2);
        }

        var result = await this.CreateService(pageSize: 8).BrowseAsync(new AuctionListingSearch(Currency: 1, Category: 4));

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
            Assert.That(result.Page.Rows.Count, Is.EqualTo(8));
            Assert.That(result.Page.TotalCount, Is.GreaterThanOrEqualTo(9));
        });
    }

    /// <summary>
    /// Verifies a price sort for fruit baskets is reported as an invalid request, not a crash.
    /// </summary>
    [Test]
    public async Task FruitPriceSortIsAnInvalidRequest()
    {
        var result = await this.CreateService().BrowseAsync(new AuctionListingSearch(Currency: 8, Sort: AuctionSearchSort.PriceAscending));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.InvalidRequest));
    }

    /// <summary>
    /// Verifies detail returns the listing, and reports a missing one as not found.
    /// </summary>
    [Test]
    public async Task DetailReturnsTheListingOrNotFound()
    {
        var listingId = await this.List(startingPrice: 1000);
        var service = this.CreateService();

        var found = await service.DetailAsync(listingId);
        var missing = await service.DetailAsync(Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(found.Result, Is.EqualTo(AuctionResultCode.Success));
            Assert.That(found.Listing!.ListingId, Is.EqualTo(listingId));
            Assert.That(missing.Result, Is.EqualTo(AuctionResultCode.ListingNotFound));
        });
    }

    /// <summary>
    /// Verifies open reports the number of collections the account has not finished claiming.
    /// </summary>
    [Test]
    public async Task OpenReportsPendingMailbox()
    {
        var now = DateTimeOffset.UtcNow;
        await using (var context = new EntityDataContext())
        {
            context.Set<AuctionCollection>().Add(new AuctionCollection
            {
                Id = Guid.NewGuid(),
                AccountId = this._sellerAccountId,
                Kind = 3,
                Currency = 0,
                OriginalScalar = 1000,
                RemainingScalar = 1000,
                Status = 0,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0,
            });
            await context.SaveChangesAsync();
        }

        var result = await this.CreateService().OpenAsync(this._sellerAccountId);

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
            Assert.That(result.PendingMailbox, Is.EqualTo(1));
        });
    }

    private AuctionHouseService CreateService(bool enabled = true, int pageSize = 8)
    {
        return new AuctionHouseService(new AuctionHouseRepository(), new AuctionHouseOptions
        {
            Enabled = enabled,
            PageSize = pageSize,
        });
    }

    private async Task<Guid> List(long startingPrice)
    {
        var now = DateTimeOffset.UtcNow;
        var created = await new AuctionHouseRepository().CreateListingAsync(new EntityDataContext(), new AuctionListingDraft(
            OperationId: Guid.NewGuid(),
            AccountId: this._sellerAccountId,
            ItemId: Guid.NewGuid(),
            SellerCharacterId: Guid.NewGuid(),
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            Currency: 1,
            Category: 4,
            StartingPrice: startingPrice,
            CreatedAt: now,
            EndsAt: now.AddHours(24)));
        return created.ListingId;
    }

    /// <summary>
    /// Verifies the item loader returns a stored item with its definition, so browse rows can serialize it.
    /// </summary>
    [Test]
    public async Task LoadItemsReturnsTheStoredItem()
    {
        await using var context = new EntityDataContext();
        var itemId = await context.Set<Item>().Where(i => i.DefinitionId != null).Select(i => i.Id).FirstOrDefaultAsync();
        if (itemId == Guid.Empty)
        {
            Assert.Ignore("The database has no item; use the populated copy.");
        }

        var items = await new AuctionHouseRepository().GetItemsAsync(new[] { itemId });

        Assert.That(items.ContainsKey(itemId), Is.True);
        Assert.That(items[itemId].Definition, Is.Not.Null);
    }

    /// <summary>
    /// Verifies my listings returns the seller's own listings with their status, and nobody else's.
    /// </summary>
    [Test]
    public async Task MyListingsReturnsOnlyTheSellersListings()
    {
        var listingId = await this.List(startingPrice: 1000);
        var service = this.CreateService();

        var mine = await service.MyListingsAsync(this._sellerAccountId, 1, status: null);
        var others = await service.MyListingsAsync(Guid.NewGuid(), 1, status: null);

        Assert.Multiple(() =>
        {
            Assert.That(mine.Result, Is.EqualTo(AuctionResultCode.Success));
            Assert.That(mine.Page.Rows.Single().ListingId, Is.EqualTo(listingId));
            Assert.That(mine.Page.Rows.Single().Status, Is.EqualTo(0));
            Assert.That(others.Page.Rows, Is.Empty);
        });
    }
}
