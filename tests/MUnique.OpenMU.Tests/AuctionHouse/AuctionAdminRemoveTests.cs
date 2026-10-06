// <copyright file="AuctionAdminRemoveTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies administrative removal against PostgreSQL: the listing ends as removed, its item returns to the seller, its escrowed
/// bid is refunded, an audit row records the administrator and reason, and a second removal is refused.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionAdminRemoveTests
{
    private string _connectionString = string.Empty;
    private Guid _sellerAccountId;
    private Guid _bidderAccountId;
    private Guid _bidderCharacterId;
    private Guid _listingId;

    [OneTimeSetUp]
    public void SetUp()
    {
        if (AuctionPostgresTestEnvironment.ConnectionString is null)
        {
            Assert.Ignore($"Set {AuctionPostgresTestEnvironment.ConnectionStringVariable} to run the PostgreSQL integration tests.");
        }

        this._connectionString = AuctionPostgresTestEnvironment.ConnectionString!;
        AuctionPostgresTestEnvironment.EnsureConfigured();
    }

    [SetUp]
    public async Task Arrange()
    {
        var owners = await AuctionTestCharacters.FundOwnedAsync(this._connectionString, 10_000_000, 1);
        if (owners.Count == 0)
        {
            Assert.Ignore("The database has no linked character; use the populated copy.");
        }

        this._sellerAccountId = Guid.NewGuid();
        this._bidderAccountId = owners[0].AccountId;
        this._bidderCharacterId = owners[0].CharacterId;
        var now = DateTimeOffset.UtcNow;
        var created = await new AuctionHouseRepository().CreateListingAsync(new EntityDataContext(), new AuctionListingDraft(
            OperationId: Guid.NewGuid(),
            AccountId: this._sellerAccountId,
            ItemId: Guid.NewGuid(),
            SellerCharacterId: Guid.NewGuid(),
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            Currency: 0,
            Category: 0,
            StartingPrice: 1000,
            CreatedAt: now,
            EndsAt: now.AddHours(24)));
        this._listingId = created.ListingId;
        await new AuctionHouseRepository().PlaceScalarBidAsync(new EntityDataContext(), new ScalarBidDraft(
            OperationId: Guid.NewGuid(),
            BidderAccountId: this._bidderAccountId,
            BidderCharacterId: this._bidderCharacterId,
            ListingId: this._listingId,
            ExpectedVersion: 0,
            BidAmount: 1000,
            MinimumBid: 1000,
            BidderIp: "10.0.0.2",
            Now: now,
            NewEndsAt: now.AddHours(24)));
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionTransactionLog>().Where(a => a.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionNotification>().Where(n => n.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionCurrencyMovement>().Where(m => m.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => c.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => b.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.Id == this._listingId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies an administrative removal returns the item, refunds the escrowed bid, and records the administrator and reason.
    /// </summary>
    [Test]
    public async Task RemovalReturnsTheItemRefundsTheBidAndIsAudited()
    {
        var outcome = await new AuctionHouseRepository().AdminRemoveAsync(new EntityDataContext(), new AdminRemoveDraft(this._listingId, "admin-42", "policy", DateTimeOffset.UtcNow));

        Assert.That(outcome, Is.EqualTo(AdminRemoveOutcome.Removed));
        await using var context = new EntityDataContext();
        var listing = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == this._listingId);
        var item = await context.Set<AuctionCollection>().AsNoTracking().CountAsync(c => c.ListingId == this._listingId && c.Kind == 1 && c.AccountId == this._sellerAccountId);
        var refund = await context.Set<AuctionCollection>().AsNoTracking().CountAsync(c => c.ListingId == this._listingId && c.Kind == 3 && c.AccountId == this._bidderAccountId);
        var audit = await context.Set<AuctionTransactionLog>().AsNoTracking().SingleAsync(a => a.ListingId == this._listingId && a.Action == 7);
        Assert.Multiple(() =>
        {
            Assert.That(listing.Status, Is.EqualTo(4));
            Assert.That(item, Is.EqualTo(1));
            Assert.That(refund, Is.EqualTo(1));
            Assert.That(audit.Reason, Does.Contain("admin-42"));
        });
    }

    /// <summary>
    /// Verifies a listing which has already ended cannot be removed again.
    /// </summary>
    [Test]
    public async Task SecondRemovalIsRefused()
    {
        await new AuctionHouseRepository().AdminRemoveAsync(new EntityDataContext(), new AdminRemoveDraft(this._listingId, "admin-42", "policy", DateTimeOffset.UtcNow));

        var second = await new AuctionHouseRepository().AdminRemoveAsync(new EntityDataContext(), new AdminRemoveDraft(this._listingId, "admin-42", "policy", DateTimeOffset.UtcNow));

        Assert.That(second, Is.EqualTo(AdminRemoveOutcome.NotActive));
    }
}
