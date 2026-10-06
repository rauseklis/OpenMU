// <copyright file="AuctionExpiryBatchTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies the expiry worker's batch step against PostgreSQL: due listings are finalized, a listing locked by another
/// worker is skipped rather than waited for, and a finalized listing is never finalized again.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionExpiryBatchTests
{
    private Guid _sellerAccountId;
    private List<Guid> _listings = new();

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
        this._listings = new List<Guid>();
        var past = DateTimeOffset.UtcNow.AddMinutes(-5);
        for (var i = 0; i < 3; i++)
        {
            var created = await new AuctionHouseRepository().CreateListingAsync(new EntityDataContext(), new AuctionListingDraft(
                OperationId: Guid.NewGuid(),
                AccountId: this._sellerAccountId,
                ItemId: Guid.NewGuid(),
                SellerCharacterId: Guid.NewGuid(),
                SellerName: "Seller",
                SellerIp: "127.0.0.1",
                Currency: 1,
                Category: 4,
                StartingPrice: 2,
                CreatedAt: past.AddHours(-1),
                EndsAt: past));
            this._listings.Add(created.ListingId);
        }
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionCollection>().Where(c => c.AccountId == this._sellerAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => context.Set<AuctionListing>().Any(l => l.Id == b.ListingId && l.SellerAccountId == this._sellerAccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._sellerAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.SellerAccountId == this._sellerAccountId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies the batch finalizes every due listing of the seller.
    /// </summary>
    [Test]
    public async Task BatchFinalizesDueListings()
    {
        await new AuctionHouseRepository().ExpireBatchAsync(new EntityDataContext(), DateTimeOffset.UtcNow, 50, fee => fee / 20);

        await using var context = new EntityDataContext();
        var statuses = await context.Set<AuctionListing>().AsNoTracking()
            .Where(l => l.SellerAccountId == this._sellerAccountId)
            .Select(l => l.Status)
            .ToListAsync();
        Assert.That(statuses.All(status => status != 0), Is.True);
    }

    /// <summary>
    /// Verifies a listing locked by another worker is skipped rather than waited for, and is finalized once it is free.
    /// </summary>
    [Test]
    public async Task LockedListingIsSkippedNotWaitedFor()
    {
        var locked = this._listings[0];
        await using var holder = new EntityDataContext();
        await using var lockTransaction = await holder.Database.BeginTransactionAsync();
        await holder.Set<AuctionListing>().FromSqlInterpolated($"SELECT * FROM data.\"AuctionListing\" WHERE \"Id\" = {locked} FOR UPDATE").ToListAsync();

        var skipped = await new AuctionHouseRepository().ExpireBatchAsync(new EntityDataContext(), DateTimeOffset.UtcNow, 50, fee => fee / 20);

        Assert.That(skipped, Is.EqualTo(2));
        await lockTransaction.RollbackAsync();

        var later = await new AuctionHouseRepository().ExpireBatchAsync(new EntityDataContext(), DateTimeOffset.UtcNow, 50, fee => fee / 20);
        Assert.That(later, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies a listing is finalized only once: a second batch finalizes nothing.
    /// </summary>
    [Test]
    public async Task FinalizedListingIsNotFinalizedAgain()
    {
        await new AuctionHouseRepository().ExpireBatchAsync(new EntityDataContext(), DateTimeOffset.UtcNow, 50, fee => fee / 20);

        var second = await new AuctionHouseRepository().ExpireBatchAsync(new EntityDataContext(), DateTimeOffset.UtcNow, 50, fee => fee / 20);

        Assert.That(second, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies a listing whose finalization throws is rolled back on its own: the other due listings still finalize, and the
    /// poisoned listing stays due.
    /// </summary>
    [Test]
    public async Task PoisonedListingIsRolledBackAloneAndStaysDue()
    {
        var funded = await AuctionTestCharacters.FundAsync(AuctionPostgresTestEnvironment.ConnectionString!, 10_000_000);
        if (funded is null)
        {
            Assert.Ignore("The database has no characters to bid with; use the populated copy.");
        }

        var repository = new AuctionHouseRepository();
        var now = DateTimeOffset.UtcNow;
        var endsAt = now.AddHours(1);
        var poisoned = await repository.CreateListingAsync(new EntityDataContext(), new AuctionListingDraft(
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
            EndsAt: endsAt));
        await repository.PlaceScalarBidAsync(new EntityDataContext(), new ScalarBidDraft(
            OperationId: Guid.NewGuid(),
            BidderAccountId: Guid.NewGuid(),
            BidderCharacterId: funded.Value,
            ListingId: poisoned.ListingId,
            ExpectedVersion: 0,
            BidAmount: 1500,
            MinimumBid: 1000,
            BidderIp: "127.0.0.1",
            Now: now,
            NewEndsAt: endsAt));

        await repository.ExpireBatchAsync(new EntityDataContext(), endsAt.AddMinutes(1), 50, _ => throw new InvalidOperationException("poisoned"));

        await using var context = new EntityDataContext();
        var poisonedStatus = await context.Set<AuctionListing>().AsNoTracking().Where(l => l.Id == poisoned.ListingId).Select(l => l.Status).SingleAsync();
        var otherStatuses = await context.Set<AuctionListing>().AsNoTracking().Where(l => this._listings.Contains(l.Id)).Select(l => l.Status).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(poisonedStatus, Is.EqualTo(0));
            Assert.That(otherStatuses.All(status => status != 0), Is.True);
        });
    }
}
