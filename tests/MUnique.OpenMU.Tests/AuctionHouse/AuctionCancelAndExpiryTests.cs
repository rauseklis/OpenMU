// <copyright file="AuctionCancelAndExpiryTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies cancellation and expiry against PostgreSQL. A cancelled or expired-without-bid listing returns its item to the
/// seller; an expired listing with a bid sells to the high bidder; a listing is finalized only once.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionCancelAndExpiryTests
{
    private const long StartingPrice = 1000;
    private const long SuccessFee = 50;
    private Guid _sellerAccountId;
    private Guid _bidderAccountId;
    private Guid _characterId;
    private Guid _listingId;
    private Guid _itemId;
    private int _version;

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
        var owners = await AuctionTestCharacters.FundOwnedAsync(AuctionPostgresTestEnvironment.ConnectionString!, 10_000_000, 1);
        if (owners.Count == 0)
        {
            Assert.Ignore("The database has no characters to bid with; use the populated copy.");
        }

        this._characterId = owners[0].CharacterId;
        this._bidderAccountId = owners[0].AccountId;
        this._sellerAccountId = Guid.NewGuid();
                this._itemId = Guid.NewGuid();
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
            StartingPrice: StartingPrice,
            CreatedAt: now,
            EndsAt: now.AddHours(24)));
        this._listingId = created.ListingId;
        this._version = 0;
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionCollection>().Where(c => c.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => b.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.Id == this._listingId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies the seller can cancel a listing with no bids, and gets its item back through the collection.
    /// </summary>
    [Test]
    public async Task SellerCancelsListingWithoutBidsAndGetsItemBack()
    {
        var result = await this.Cancel(this._sellerAccountId, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionCancelOutcome.Cancelled));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionListing>().AsNoTracking().Where(l => l.Id == this._listingId).Select(l => l.Status).SingleAsync(), Is.EqualTo(3));
        var returned = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.ListingId == this._listingId && c.Kind == 1);
        Assert.Multiple(() =>
        {
            Assert.That(returned.AccountId, Is.EqualTo(this._sellerAccountId));
            Assert.That(returned.ItemId, Is.EqualTo(this._itemId));
        });
    }

    /// <summary>
    /// Verifies a listing with a bid cannot be cancelled.
    /// </summary>
    [Test]
    public async Task ListingWithBidCannotBeCancelled()
    {
        await this.Bid(this._bidderAccountId, StartingPrice, this._version, Guid.NewGuid());
        this._version++;

        var result = await this.Cancel(this._sellerAccountId, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionCancelOutcome.CannotCancelWithBid));
    }

    /// <summary>
    /// Verifies only the seller can cancel.
    /// </summary>
    [Test]
    public async Task NonSellerCannotCancel()
    {
        var result = await this.Cancel(this._bidderAccountId, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionCancelOutcome.NotSeller));
    }

    /// <summary>
    /// Verifies a replayed cancellation returns the stored outcome and returns the item only once.
    /// </summary>
    [Test]
    public async Task ReplayedCancellationReturnsTheItemOnce()
    {
        var operation = Guid.NewGuid();
        await this.Cancel(this._sellerAccountId, this._version, operation);

        var replay = await this.Cancel(this._sellerAccountId, this._version, operation);

        Assert.That(replay.Outcome, Is.EqualTo(AuctionCancelOutcome.Replayed));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionCollection>().CountAsync(c => c.ListingId == this._listingId && c.Kind == 1), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies an expired listing with no bid returns its item to the seller and is marked expired.
    /// </summary>
    [Test]
    public async Task ExpiredListingWithoutBidReturnsItemToSeller()
    {
        await this.MakeDue();

        var result = await this.Expire(SuccessFee);

        Assert.That(result.Outcome, Is.EqualTo(AuctionExpireOutcome.Finalized));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionListing>().AsNoTracking().Where(l => l.Id == this._listingId).Select(l => l.Status).SingleAsync(), Is.EqualTo(2));
        Assert.That(await context.Set<AuctionCollection>().CountAsync(c => c.ListingId == this._listingId && c.Kind == 1 && c.AccountId == this._sellerAccountId), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies an expired listing with a bid sells to the high bidder, and the seller receives the proceeds less the fee.
    /// </summary>
    [Test]
    public async Task ExpiredListingWithBidSellsToHighBidder()
    {
        await this.Bid(this._bidderAccountId, StartingPrice, this._version, Guid.NewGuid());
        this._version++;
        await this.MakeDue();

        var result = await this.Expire(SuccessFee);

        Assert.That(result.Outcome, Is.EqualTo(AuctionExpireOutcome.Finalized));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionListing>().AsNoTracking().Where(l => l.Id == this._listingId).Select(l => l.Status).SingleAsync(), Is.EqualTo(1));
        var item = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.ListingId == this._listingId && c.Kind == 0);
        var proceeds = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.ListingId == this._listingId && c.Kind == 2);
        Assert.Multiple(() =>
        {
            Assert.That(item.AccountId, Is.EqualTo(this._bidderAccountId));
            Assert.That(proceeds.RemainingScalar, Is.EqualTo(StartingPrice - SuccessFee));
        });
    }

    /// <summary>
    /// Verifies a listing that has not yet expired is not finalized.
    /// </summary>
    [Test]
    public async Task ListingNotYetDueIsNotFinalized()
    {
        var result = await this.Expire(SuccessFee);

        Assert.That(result.Outcome, Is.EqualTo(AuctionExpireOutcome.NotDue));
    }

    /// <summary>
    /// Verifies a listing is finalized only once: a second expiry finds nothing due.
    /// </summary>
    [Test]
    public async Task ListingIsFinalizedOnlyOnce()
    {
        await this.MakeDue();
        await this.Expire(SuccessFee);

        var second = await this.Expire(SuccessFee);

        Assert.That(second.Outcome, Is.EqualTo(AuctionExpireOutcome.NotDue));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionCollection>().CountAsync(c => c.ListingId == this._listingId && c.Kind == 1), Is.EqualTo(1));
    }

    private async Task MakeDue()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionListing>().Where(l => l.Id == this._listingId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.EndsAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    private Task<AuctionCancelResult> Cancel(Guid account, int expectedVersion, Guid operationId)
    {
        return new AuctionHouseRepository().CancelAsync(new EntityDataContext(), new CancelDraft(
            OperationId: operationId,
            AccountId: account,
            ListingId: this._listingId,
            ExpectedVersion: expectedVersion,
            Now: DateTimeOffset.UtcNow));
    }

    private Task<AuctionExpireResult> Expire(long successFee)
    {
        return new AuctionHouseRepository().ExpireAsync(new EntityDataContext(), new ExpireDraft(
            ListingId: this._listingId,
            SuccessFee: successFee,
            Now: DateTimeOffset.UtcNow));
    }

    private Task<AuctionBidResult> Bid(Guid bidder, long amount, int expectedVersion, Guid operationId)
    {
        var now = DateTimeOffset.UtcNow;
        return new AuctionHouseRepository().PlaceScalarBidAsync(new EntityDataContext(), new ScalarBidDraft(
            OperationId: operationId,
            BidderAccountId: bidder,
            BidderCharacterId: this._characterId,
            ListingId: this._listingId,
            ExpectedVersion: expectedVersion,
            BidAmount: amount,
            MinimumBid: StartingPrice,
            BidderIp: "127.0.0.1",
            Now: now,
            NewEndsAt: now.AddHours(24)));
    }
}
