// <copyright file="AuctionRepairTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies the return-collection repair against PostgreSQL: an ended listing whose item is outside every inventory gets its
/// missing return collection with an audit row, and an item still inside an inventory is left for manual review.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionRepairTests
{
    private Guid _sellerAccountId;
    private Guid _detachedListing;
    private Guid _detachedItem;
    private Guid _attachedListing;
    private Guid _attachedItem;
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
        this._detachedListing = Guid.NewGuid();
        this._attachedListing = Guid.NewGuid();
        this._detachedItem = Guid.NewGuid();
        this._attachedItem = Guid.NewGuid();
        await using var context = new EntityDataContext();
        var definitionId = await context.Set<Item>().Where(i => i.DefinitionId != null).Select(i => i.DefinitionId).FirstOrDefaultAsync();
        this._inventoryId = (await context.Set<Item>().Where(i => i.ItemStorageId != null).Select(i => i.ItemStorageId).FirstOrDefaultAsync()) ?? Guid.Empty;
        if (definitionId is null || this._inventoryId == Guid.Empty)
        {
            Assert.Ignore("The database has no item or storage; use the populated copy.");
        }

        context.Set<Item>().Add(new Item { Id = this._detachedItem, ItemStorageId = null, ItemSlot = 0, DefinitionId = definitionId, Durability = 1 });
        context.Set<Item>().Add(new Item { Id = this._attachedItem, ItemStorageId = this._inventoryId, ItemSlot = 77, DefinitionId = definitionId, Durability = 1 });
        var now = DateTimeOffset.UtcNow.AddHours(-1);
        context.Set<AuctionListing>().AddRange(Ended(this._detachedListing, this._detachedItem, this._sellerAccountId, now), Ended(this._attachedListing, this._attachedItem, this._sellerAccountId, now));
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionTransactionLog>().Where(a => a.ActorAccountId == Guid.Empty && a.CounterpartAccountId == this._sellerAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => c.AccountId == this._sellerAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.Id == this._detachedListing || l.Id == this._attachedListing).ExecuteDeleteAsync();
        await context.Set<Item>().Where(i => i.Id == this._detachedItem || i.Id == this._attachedItem).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies the repair creates the missing return collection for a detached item, writes an audit row, and skips the item still in an inventory.
    /// </summary>
    [Test]
    public async Task RepairCreatesTheReturnCollectionOnlyWhereTheItemIsDetached()
    {
        var result = await new AuctionHouseRepository().RepairMissingReturnCollectionsAsync(new EntityDataContext(), DateTimeOffset.UtcNow, 5000);

        Assert.That(result.Repaired, Is.GreaterThanOrEqualTo(1));
        await using var context = new EntityDataContext();
        var returned = await context.Set<AuctionCollection>().AsNoTracking().CountAsync(c => c.ListingId == this._detachedListing && c.Kind == 1);
        var attached = await context.Set<AuctionCollection>().AsNoTracking().CountAsync(c => c.ListingId == this._attachedListing && c.Kind == 1);
        var audit = await context.Set<AuctionTransactionLog>().AsNoTracking().CountAsync(a => a.ListingId == this._detachedListing && a.Action == 6);
        Assert.Multiple(() =>
        {
            Assert.That(returned, Is.EqualTo(1));
            Assert.That(attached, Is.EqualTo(0));
            Assert.That(audit, Is.EqualTo(1));
        });
    }

    private static AuctionListing Ended(Guid id, Guid itemId, Guid seller, DateTimeOffset when)
        => new()
        {
            Id = id,
            ItemId = itemId,
            SellerAccountId = seller,
            SellerCharacterId = Guid.NewGuid(),
            SellerName = "Seller",
            Status = 2,
            Currency = 0,
            Category = 0,
            StartingPrice = 1000,
            CreatedAt = when,
            OriginalEndsAt = when,
            EndsAt = when,
            NormalizedName = string.Empty,
        };
}
