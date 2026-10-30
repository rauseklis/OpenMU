// <copyright file="AuctionBidLimitTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Threading;
using Moq;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Verifies that a bid is refused before it reaches the database when it is outside the price range of its currency or
/// is not below the buyout price. These tests use a fake repository and need no database.
/// </summary>
[TestFixture]
public class AuctionBidLimitTests
{
    private const long BuyoutPrice = 5_000_000;

    private Mock<IAuctionHouseRepository> _repository = null!;

    [SetUp]
    public void SetUp()
    {
        this._repository = new Mock<IAuctionHouseRepository>();
        this._repository.Setup(r => r.PlaceScalarBidAsync(It.IsAny<ScalarBidDraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuctionBidResult(AuctionBidOutcome.Placed));
    }

    /// <summary>
    /// A bid above the largest Zen price must be refused; in the database it would wrap around into a negative debit and give
    /// the bidder Zen.
    /// </summary>
    /// <param name="bid">The bid.</param>
    [TestCase(2_000_000_001L)]
    [TestCase(3_000_000_000L)]
    [TestCase(4_294_967_295L)]
    public async Task ABidAboveTheLargestZenPriceIsRefused(long bid)
    {
        var result = await this.CreateService(this.Listing(buyoutPrice: null)).BidAsync(this.Bid(bid));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.InvalidPrice));
        this._repository.Verify(r => r.PlaceScalarBidAsync(It.IsAny<ScalarBidDraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A bid at or above the buyout price is refused: the bidder should buy the item out instead of paying more.
    /// </summary>
    /// <param name="bid">The bid.</param>
    [TestCase(BuyoutPrice)]
    [TestCase(BuyoutPrice + 1000)]
    public async Task ABidAtOrAboveTheBuyoutPriceIsRefused(long bid)
    {
        var result = await this.CreateService(this.Listing(buyoutPrice: BuyoutPrice)).BidAsync(this.Bid(bid));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.InvalidPrice));
        this._repository.Verify(r => r.PlaceScalarBidAsync(It.IsAny<ScalarBidDraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// An ordinary bid below the buyout price still reaches the repository.
    /// </summary>
    [Test]
    public async Task ABidBelowTheBuyoutPriceIsPlaced()
    {
        var result = await this.CreateService(this.Listing(buyoutPrice: BuyoutPrice)).BidAsync(this.Bid(BuyoutPrice - 1000));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        this._repository.Verify(r => r.PlaceScalarBidAsync(It.IsAny<ScalarBidDraft>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The largest allowed Zen bid is still accepted when the listing has no buyout.
    /// </summary>
    [Test]
    public async Task TheLargestZenPriceIsAccepted()
    {
        var result = await this.CreateService(this.Listing(buyoutPrice: null)).BidAsync(this.Bid(AuctionRules.MaximumZenPrice));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
    }

    private AuctionHouseService CreateService(AuctionListingDetail listing)
    {
        this._repository.Setup(r => r.GetListingAsync(listing.ListingId, It.IsAny<CancellationToken>())).ReturnsAsync(listing);
        return new AuctionHouseService(this._repository.Object, new AuctionHouseOptions { Enabled = true });
    }

    private AuctionBidRequest Bid(long amount)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), this._listingId, 0, amount, "127.0.0.1");

    private Guid _listingId = Guid.NewGuid();

    private AuctionListingDetail Listing(long? buyoutPrice)
    {
        var now = DateTimeOffset.UtcNow;
        return new AuctionListingDetail(
            this._listingId, 0, 0, 0, 0, Guid.NewGuid(), 1000, buyoutPrice, null, now.AddHours(1), now.AddHours(1), 0, "Seller", null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
    }
}
