// <copyright file="AuctionBuyoutTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies buyout against PostgreSQL: the listing is sold at its buyout price, the buyer receives the item, the seller
/// receives the proceeds less the success fee, and any displaced high bid is refunded.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionBuyoutTests
{
    private const long BuyoutPrice = 5000;
    private const long SuccessFee = 250;
    private Guid _sellerAccountId;
    private Guid _buyerAccountId;
    private Guid _otherAccountId;
    private Guid _characterId;
    private Guid _otherCharacterId;
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
        var owners = await AuctionTestCharacters.FundOwnedAsync(AuctionPostgresTestEnvironment.ConnectionString!, 10_000_000, 2);
        if (owners.Count == 0)
        {
            Assert.Ignore("The database has no characters to bid with; use the populated copy.");
        }

        this._characterId = owners[0].CharacterId;
        this._buyerAccountId = owners[0].AccountId;
        this._otherCharacterId = owners[1].CharacterId;
        this._sellerAccountId = Guid.NewGuid();
        this._otherAccountId = owners[1].AccountId;
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
            StartingPrice: 1000,
            CreatedAt: now,
            EndsAt: now.AddHours(24),
            BuyoutPrice: BuyoutPrice));
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
    /// Verifies a buyout with no bids sells the listing, gives the buyer the item and the seller the net proceeds.
    /// </summary>
    [Test]
    public async Task BuyoutSellsTheListingAndSettlesBothSides()
    {
        var result = await this.Buy(this._buyerAccountId, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        await using var context = new EntityDataContext();
        var listing = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == this._listingId);
        Assert.That(listing.Status, Is.EqualTo(1));
        var item = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.ListingId == this._listingId && c.Kind == 0);
        var proceeds = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.ListingId == this._listingId && c.Kind == 2);
        Assert.Multiple(() =>
        {
            Assert.That(item.AccountId, Is.EqualTo(this._buyerAccountId));
            Assert.That(item.ItemId, Is.EqualTo(this._itemId));
            Assert.That(proceeds.AccountId, Is.EqualTo(this._sellerAccountId));
            Assert.That(proceeds.RemainingScalar, Is.EqualTo(BuyoutPrice - SuccessFee));
        });
    }

    /// <summary>
    /// Verifies the current high bidder who buys out consumes its own escrow rather than being refunded.
    /// </summary>
    [Test]
    public async Task HighBidderBuyingOutConsumesItsEscrow()
    {
        await this.Bid(this._buyerAccountId, 1000, this._version, Guid.NewGuid());
        this._version++;

        var result = await this.Buy(this._buyerAccountId, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionCollection>().CountAsync(c => c.ListingId == this._listingId && c.Kind == 3), Is.EqualTo(0));
        Assert.That(await context.Set<AuctionBid>().CountAsync(b => b.ListingId == this._listingId && b.Status == 2), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies a different buyer's buyout refunds the displaced high bidder in full.
    /// </summary>
    [Test]
    public async Task OtherBuyerRefundsTheDisplacedHighBidder()
    {
        await this.Bid(this._otherAccountId, 1000, this._version, Guid.NewGuid());
        this._version++;

        var result = await this.Buy(this._buyerAccountId, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        await using var context = new EntityDataContext();
        var refund = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.ListingId == this._listingId && c.Kind == 3);
        Assert.Multiple(() =>
        {
            Assert.That(refund.AccountId, Is.EqualTo(this._otherAccountId));
            Assert.That(refund.RemainingScalar, Is.EqualTo(1000));
        });
    }

    /// <summary>
    /// Verifies a buyout is refused when the listing has no buyout price.
    /// </summary>
    [Test]
    public async Task ListingWithoutBuyoutCannotBeBoughtOut()
    {
        await using (var context = new EntityDataContext())
        {
            await context.Set<AuctionListing>().Where(l => l.Id == this._listingId).ExecuteUpdateAsync(s => s.SetProperty(l => l.BuyoutPrice, (long?)null));
        }

        var result = await this.Buy(this._buyerAccountId, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.BuyoutUnavailable));
    }

    /// <summary>
    /// Verifies a buyout against an outdated version is refused without changing anything.
    /// </summary>
    [Test]
    public async Task StaleVersionIsRefusedWithoutChange()
    {
        var result = await this.Buy(this._buyerAccountId, this._version + 3, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.StaleListing));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionListing>().AsNoTracking().Where(l => l.Id == this._listingId).Select(l => l.Status).SingleAsync(), Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies the seller cannot buy out its own listing.
    /// </summary>
    [Test]
    public async Task SellerCannotBuyOutItsOwnListing()
    {
        var result = await this.Buy(this._sellerAccountId, this._version, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.SelfBuy));
    }

    /// <summary>
    /// Verifies a replayed buyout returns the stored outcome and sells nothing again.
    /// </summary>
    [Test]
    public async Task ReplayedBuyoutChangesNothing()
    {
        var operation = Guid.NewGuid();
        await this.Buy(this._buyerAccountId, this._version, operation);

        var replay = await this.Buy(this._buyerAccountId, this._version, operation);

        Assert.That(replay.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Replayed));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionCollection>().CountAsync(c => c.ListingId == this._listingId && c.Kind == 0), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies a buyout and a bid racing on the same version cannot both succeed.
    /// </summary>
    [Test]
    public async Task RacingBuyoutAndBidCannotBothSucceed()
    {
        var buyoutTask = this.Buy(this._buyerAccountId, this._version, Guid.NewGuid());
        var bidTask = this.Bid(this._otherAccountId, 1000, this._version, Guid.NewGuid());

        await Task.WhenAll(buyoutTask, bidTask);

        var buyout = buyoutTask.Result.Outcome;
        var bid = bidTask.Result.Outcome;
        var winners = (buyout == AuctionBuyoutOutcome.Bought ? 1 : 0) + (bid == AuctionBidOutcome.Placed ? 1 : 0);
        var losers = (buyout == AuctionBuyoutOutcome.StaleListing ? 1 : 0) + (bid == AuctionBidOutcome.StaleListing ? 1 : 0);
        Assert.Multiple(() =>
        {
            Assert.That(winners, Is.EqualTo(1));
            Assert.That(losers, Is.EqualTo(1));
        });
    }

    private Guid CharacterOf(Guid account) => account == this._otherAccountId ? this._otherCharacterId : this._characterId;

    private Task<AuctionBuyoutResult> Buy(Guid buyer, int expectedVersion, Guid operationId)
    {
        var now = DateTimeOffset.UtcNow;
        return new AuctionHouseRepository().BuyoutAsync(new EntityDataContext(), new BuyoutDraft(
            OperationId: operationId,
            BuyerAccountId: buyer,
            BuyerCharacterId: CharacterOf(buyer),
            ListingId: this._listingId,
            ExpectedVersion: expectedVersion,
            SuccessFee: SuccessFee,
            BuyerIp: "127.0.0.1",
            Now: now));
    }

    private Task<AuctionBidResult> Bid(Guid bidder, long amount, int expectedVersion, Guid operationId)
    {
        var now = DateTimeOffset.UtcNow;
        return new AuctionHouseRepository().PlaceScalarBidAsync(new EntityDataContext(), new ScalarBidDraft(
            OperationId: operationId,
            BidderAccountId: bidder,
            BidderCharacterId: CharacterOf(bidder),
            ListingId: this._listingId,
            ExpectedVersion: expectedVersion,
            BidAmount: amount,
            MinimumBid: 1000,
            BidderIp: "127.0.0.1",
            Now: now,
            NewEndsAt: now.AddHours(24)));
    }
}
