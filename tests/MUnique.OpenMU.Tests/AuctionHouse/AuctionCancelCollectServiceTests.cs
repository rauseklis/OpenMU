// <copyright file="AuctionCancelCollectServiceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies cancelling and collecting through the service against PostgreSQL: a listing with a bid cannot be cancelled, a
/// non-seller learns only that the listing is unknown, and an unknown collection is reported as not found.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionCancelCollectServiceTests
{
    private Guid _sellerAccountId;
    private Guid _bidderAccountId;
    private Guid _listingId;

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
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionCurrencyMovement>().Where(m => m.AccountId == this._sellerAccountId || m.AccountId == this._bidderAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => c.AccountId == this._sellerAccountId || c.AccountId == this._bidderAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => b.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._sellerAccountId || o.AccountId == this._bidderAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.Id == this._listingId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies the seller can cancel a listing which has no bid.
    /// </summary>
    [Test]
    public async Task SellerCancelsListingWithoutBid()
    {
        var result = await this.CreateService().CancelAsync(this.Cancel(this._sellerAccountId, expectedVersion: 0));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
    }

    /// <summary>
    /// Verifies a listing with a bid cannot be cancelled.
    /// </summary>
    [Test]
    public async Task ListingWithBidCannotBeCancelled()
    {
        var owners = await AuctionTestCharacters.FundOwnedAsync(AuctionPostgresTestEnvironment.ConnectionString!, 10_000_000, 1);
        if (owners.Count == 0)
        {
            Assert.Ignore("The database has no character to bid with; use the populated copy.");
        }

        this._bidderAccountId = owners[0].AccountId;
        var bid = await new AuctionHouseRepository().PlaceScalarBidAsync(new EntityDataContext(), new ScalarBidDraft(
            OperationId: Guid.NewGuid(),
            BidderAccountId: this._bidderAccountId,
            BidderCharacterId: owners[0].CharacterId,
            ListingId: this._listingId,
            ExpectedVersion: 0,
            BidAmount: 1000,
            MinimumBid: 1000,
            BidderIp: "127.0.0.1",
            Now: DateTimeOffset.UtcNow,
            NewEndsAt: DateTimeOffset.UtcNow.AddHours(24)));
        Assert.That(bid.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));

        var result = await this.CreateService().CancelAsync(this.Cancel(this._sellerAccountId, expectedVersion: 1));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.CannotCancelWithBid));
    }

    /// <summary>
    /// Verifies a non-seller is told the listing does not exist, and the listing stays active.
    /// </summary>
    [Test]
    public async Task NonSellerIsToldTheListingIsUnknown()
    {
        var result = await this.CreateService().CancelAsync(this.Cancel(Guid.NewGuid(), expectedVersion: 0));

        await using var context = new EntityDataContext();
        var status = await context.Set<AuctionListing>().AsNoTracking().Where(l => l.Id == this._listingId).Select(l => l.Status).SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(AuctionResultCode.ListingNotFound));
            Assert.That(status, Is.EqualTo(0));
        });
    }

    /// <summary>
    /// Verifies collecting an unknown collection is reported as not found.
    /// </summary>
    [Test]
    public async Task UnknownCollectionIsNotFound()
    {
        var result = await this.CreateService().CollectAsync(new AuctionCollectRequest(
            OperationId: Guid.NewGuid(),
            AccountId: this._sellerAccountId,
            CharacterId: Guid.NewGuid(),
            CollectionId: Guid.NewGuid(),
            ExpectedVersion: 0,
            RequestedScalar: null,
            RequestedFruits: null));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.CollectionNotFound));
    }

    private AuctionHouseService CreateService()
    {
        return new AuctionHouseService(new AuctionHouseRepository(), new AuctionHouseOptions { Enabled = true });
    }

    private AuctionCancelRequest Cancel(Guid accountId, int expectedVersion)
    {
        return new AuctionCancelRequest(Guid.NewGuid(), accountId, this._listingId, expectedVersion);
    }
}
