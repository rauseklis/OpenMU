// <copyright file="AuctionReconciliationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies reconciliation against PostgreSQL: a listing whose item is back in an inventory is reported, and reporting changes nothing.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionReconciliationTests
{
    private Guid _sellerAccountId;
    private Guid _itemId;
    private Guid _listingId;
    private Guid _inventoryId;

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
    public async Task Arrange()
    {
        this._sellerAccountId = Guid.NewGuid();
        this._itemId = Guid.NewGuid();
        await using var context = new EntityDataContext();
        var definitionId = await context.Set<Item>().Where(i => i.DefinitionId != null).Select(i => i.DefinitionId).FirstOrDefaultAsync();
        var inventoryId = await context.Set<Item>().Where(i => i.ItemStorageId != null).Select(i => i.ItemStorageId).FirstOrDefaultAsync();
        if (definitionId is null || inventoryId is null)
        {
            Assert.Ignore("The database has no item or storage; use the populated copy.");
        }

        this._inventoryId = inventoryId.Value;
        context.Set<Item>().Add(new Item { Id = this._itemId, ItemStorageId = null, ItemSlot = 0, DefinitionId = definitionId, Durability = 10 });
        await context.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var created = await new AuctionHouseRepository().CreateListingAsync(new EntityDataContext(), new AuctionListingDraft(
            OperationId: Guid.NewGuid(),
            AccountId: this._sellerAccountId,
            ItemId: this._itemId,
            SellerCharacterId: Guid.NewGuid(),
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            Currency: 0,
            Category: 0,
            StartingPrice: 1000,
            CreatedAt: now,
            EndsAt: now.AddHours(24)));
        this._listingId = created.ListingId;
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._sellerAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.Id == this._listingId).ExecuteDeleteAsync();
        await context.Set<Item>().Where(i => i.Id == this._itemId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies a listing whose item is back in an inventory is reported, and the report does not change the data.
    /// </summary>
    [Test]
    public async Task ItemBackInInventoryIsReportedWithoutRepair()
    {
        await using (var context = new EntityDataContext())
        {
            await context.Set<Item>().Where(i => i.Id == this._itemId).ExecuteUpdateAsync(s => s.SetProperty(i => i.ItemStorageId, this._inventoryId));
        }

        var findings = await new AuctionHouseRepository().ReconcileAsync();

        Assert.That(findings.Any(f => f.Invariant == "ActiveListingItemStillInInventory" && f.Subject == this._listingId.ToString()), Is.True);
        await using var check = new EntityDataContext();
        Assert.That(await check.Set<Item>().AsNoTracking().Where(i => i.Id == this._itemId).Select(i => i.ItemStorageId).SingleAsync(), Is.EqualTo(this._inventoryId));
    }

    /// <summary>
    /// Verifies a fruit listing whose recorded current bid does not match its escrowed fruits is reported per component.
    /// </summary>
    [Test]
    public async Task FruitEscrowMismatchIsReportedPerComponent()
    {
        var listing = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var context = new EntityDataContext())
        {
            context.Set<AuctionListing>().Add(new AuctionListing
            {
                Id = listing,
                ItemId = Guid.NewGuid(),
                SellerAccountId = Guid.NewGuid(),
                SellerCharacterId = Guid.NewGuid(),
                SellerName = "Seller",
                Status = 0,
                Currency = 8,
                CreatedAt = now,
                OriginalEndsAt = now.AddHours(24),
                EndsAt = now.AddHours(24),
                NormalizedName = string.Empty,
                CurrentBidStrength = 7,
                BidCount = 1,
            });
            await context.SaveChangesAsync();
        }

        try
        {
            var findings = await new AuctionHouseRepository().ReconcileAsync();

            Assert.That(findings.Any(f => f.Invariant == "FruitEscrowTotalsDifferFromBidObligations" && f.Subject == "Strength"), Is.True);
        }
        finally
        {
            await using var cleanup = new EntityDataContext();
            await cleanup.Set<AuctionListing>().Where(l => l.Id == listing).ExecuteDeleteAsync();
        }
    }
}
