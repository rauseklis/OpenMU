// <copyright file="AuctionBidTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies scalar bidding against PostgreSQL: the bid is escrowed, the displaced bid becomes a refund, a stale version
/// is refused, a seller cannot bid on its own listing, a replay changes nothing, and two racing bids cannot both win.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionBidTests
{
    private Guid _sellerAccountId;
    private Guid _bidderOne;
    private Guid _bidderTwo;
    private Guid _characterId;
    private Guid _listingId;
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
        var funded = await AuctionTestCharacters.FundAsync(AuctionPostgresTestEnvironment.ConnectionString!, 10_000_000);
        if (funded is null)
        {
            Assert.Ignore("The database has no characters to bid with; use the populated copy.");
        }

        this._characterId = funded.Value;
        this._sellerAccountId = Guid.NewGuid();
        this._bidderOne = Guid.NewGuid();
        this._bidderTwo = Guid.NewGuid();
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
    /// Verifies a first bid is escrowed and the listing records it.
    /// </summary>
    [Test]
    public async Task FirstBidIsEscrowedAndRecorded()
    {
        var result = await this.Bid(this._bidderOne, 1000, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));
        await using var context = new EntityDataContext();
        var listing = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == this._listingId);
        Assert.Multiple(() =>
        {
            Assert.That(listing.CurrentBid, Is.EqualTo(1000));
            Assert.That(listing.BidCount, Is.EqualTo(1));
            Assert.That(listing.Version, Is.EqualTo(this._version + 1));
        });
        Assert.That(await context.Set<AuctionBid>().CountAsync(b => b.ListingId == this._listingId && b.Status == 0), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies the displaced bid is released and becomes a refund collection for its bidder.
    /// </summary>
    [Test]
    public async Task DisplacedBidBecomesARefundCollection()
    {
        await this.Bid(this._bidderOne, 1000, this._version, Guid.NewGuid());
        this._version++;

        var result = await this.Bid(this._bidderTwo, 1100, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));
        await using var context = new EntityDataContext();
        var refund = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.ListingId == this._listingId && c.Kind == 3);
        Assert.Multiple(() =>
        {
            Assert.That(refund.AccountId, Is.EqualTo(this._bidderOne));
            Assert.That(refund.RemainingScalar, Is.EqualTo(1000));
        });
        Assert.That(await context.Set<AuctionBid>().CountAsync(b => b.ListingId == this._listingId && b.Status == 1), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies a bid against an outdated version is refused without changing anything.
    /// </summary>
    [Test]
    public async Task StaleVersionIsRefusedWithoutChange()
    {
        var result = await this.Bid(this._bidderOne, 1000, this._version + 5, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.StaleListing));
        await using var context = new EntityDataContext();
        var listing = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == this._listingId);
        Assert.Multiple(() =>
        {
            Assert.That(listing.BidCount, Is.EqualTo(0));
            Assert.That(listing.Version, Is.EqualTo(this._version));
        });
    }

    /// <summary>
    /// Verifies the seller cannot bid on its own listing.
    /// </summary>
    [Test]
    public async Task SellerCannotBidOnItsOwnListing()
    {
        var result = await this.Bid(this._sellerAccountId, 1000, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.SelfBid));
    }

    /// <summary>
    /// Verifies a replayed bid returns the stored outcome and places nothing new.
    /// </summary>
    [Test]
    public async Task ReplayedBidChangesNothing()
    {
        var operation = Guid.NewGuid();
        await this.Bid(this._bidderOne, 1000, this._version, operation);

        var replay = await this.Bid(this._bidderOne, 1000, this._version, operation);

        Assert.That(replay.Outcome, Is.EqualTo(AuctionBidOutcome.Replayed));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionBid>().CountAsync(b => b.ListingId == this._listingId), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies two bids racing on the same version cannot both win: exactly one is placed.
    /// </summary>
    [Test]
    public async Task RacingBidsOnTheSameVersionCannotBothWin()
    {
        var bids = Enumerable.Range(0, 2)
            .Select(i => this.Bid(i == 0 ? this._bidderOne : this._bidderTwo, 1000, this._version, Guid.NewGuid()))
            .ToArray();

        var results = await Task.WhenAll(bids);

        Assert.That(results.Count(r => r.Outcome == AuctionBidOutcome.Placed), Is.EqualTo(1));
        Assert.That(results.Count(r => r.Outcome == AuctionBidOutcome.StaleListing), Is.EqualTo(1));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionBid>().CountAsync(b => b.ListingId == this._listingId && b.Status == 0), Is.EqualTo(1));
    }

    private Task<AuctionBidResult> Bid(Guid bidder, long amount, int expectedVersion, Guid operationId)
    {
        var repository = new AuctionHouseRepository();
        var now = DateTimeOffset.UtcNow;
        return repository.PlaceScalarBidAsync(new EntityDataContext(), new ScalarBidDraft(
            OperationId: operationId,
            BidderAccountId: bidder,
            BidderCharacterId: this._characterId,
            ListingId: this._listingId,
            ExpectedVersion: expectedVersion,
            BidAmount: amount,
            MinimumBid: 1000,
            BidderIp: "127.0.0.1",
            Now: now,
            NewEndsAt: now.AddHours(24)));
    }
}
