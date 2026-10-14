// <copyright file="AuctionMixedServiceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Threading;
using Moq;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Verifies the service side of mixed-currency listings: creation is gated by the switch and validated against the price rules,
/// no percentage fee is charged, and bids and buyouts of a mixed listing go to the basket methods of the repository.
/// </summary>
[TestFixture]
public class AuctionMixedServiceTests
{
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly Guid Character = Guid.NewGuid();
    private static readonly Guid Listing = Guid.NewGuid();

    /// <summary>
    /// Verifies a mixed listing is refused while the switch is off, and nothing is sent to the repository.
    /// </summary>
    [Test]
    public async Task MixedListingIsRefusedWhileMixedPricesAreOff()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = true, MixedPricesEnabled = false });

        var result = await service.ListItemAsync(MixedRequest(Basket((PriceKind.Soul, 3), (PriceKind.Zen, 5_000)), null));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.UnsupportedCurrency));
        repository.Verify(r => r.ListItemAsync(It.IsAny<ItemListingDraft>(), It.IsAny<Func<ItemListingFacts, bool>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies prices which break the price rules are refused: no price, too many kinds, a buyout below the start.
    /// </summary>
    [Test]
    public async Task MixedListingWithAnInvalidPriceIsRefused()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        var service = this.Service(repository);
        var sixKinds = Basket((PriceKind.Zen, 5_000), (PriceKind.Chaos, 1), (PriceKind.Bless, 1), (PriceKind.Soul, 1), (PriceKind.Life, 1), (PriceKind.Creation, 1));

        var none = await service.ListItemAsync(MixedRequest(null, null));
        var tooMany = await service.ListItemAsync(MixedRequest(sixKinds, null));
        var below = await service.ListItemAsync(MixedRequest(Basket((PriceKind.Soul, 5)), Basket((PriceKind.Soul, 4))));

        Assert.Multiple(() =>
        {
            Assert.That(none.Result, Is.EqualTo(AuctionResultCode.InvalidPrice));
            Assert.That(tooMany.Result, Is.EqualTo(AuctionResultCode.InvalidPrice));
            Assert.That(below.Result, Is.EqualTo(AuctionResultCode.InvalidPrice));
        });
        repository.Verify(r => r.ListItemAsync(It.IsAny<ItemListingDraft>(), It.IsAny<Func<ItemListingFacts, bool>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies a valid mixed auction and a buy-now listing reach the repository with their baskets, the flat sale fee and no listing fee.
    /// </summary>
    [Test]
    public async Task MixedListingPassesTheBasketsAndChargesOnlyTheFlatFee()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        var drafts = new List<ItemListingDraft>();
        repository.Setup(r => r.ListItemAsync(It.IsAny<ItemListingDraft>(), It.IsAny<Func<ItemListingFacts, bool>>(), It.IsAny<CancellationToken>()))
            .Callback<ItemListingDraft, Func<ItemListingFacts, bool>, CancellationToken>((d, _, _) => drafts.Add(d))
            .ReturnsAsync(new AuctionItemListingResult(AuctionItemListingOutcome.Created, Listing, 7));
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = true, MixedPricesEnabled = true, ListingFeeBasisPoints = 100, SaleFee = new SaleFee(SaleFeeCurrency.Zen, 100_000) });
        var start = Basket((PriceKind.Soul, 3), (PriceKind.Zen, 50_000));
        var buyout = Basket((PriceKind.Soul, 5), (PriceKind.Zen, 50_000));

        var auction = await service.ListItemAsync(MixedRequest(start, buyout));
        var buyNow = await service.ListItemAsync(MixedRequest(null, buyout));

        Assert.Multiple(() =>
        {
            Assert.That(auction.Result, Is.EqualTo(AuctionResultCode.Success));
            Assert.That(buyNow.Result, Is.EqualTo(AuctionResultCode.Success));
            Assert.That(drafts, Has.Count.EqualTo(2));
            Assert.That(drafts[0].Currency, Is.EqualTo((byte)AuctionCurrencyMode.Mixed));
            Assert.That(drafts[0].StartingBasket, Is.EqualTo(start));
            Assert.That(drafts[0].BuyoutBasket, Is.EqualTo(buyout));
            Assert.That(drafts[0].ListingFee, Is.Zero);
            Assert.That(drafts[0].SaleFee, Is.EqualTo(100_000));
            Assert.That(drafts[1].StartingBasket, Is.Null);
            Assert.That(drafts[1].BuyoutBasket, Is.EqualTo(buyout));
        });
    }

    /// <summary>
    /// Verifies a bid on a mixed listing goes to the basket bid with the configured limits, even while the listing switch is off,
    /// and a request without a basket is refused.
    /// </summary>
    [Test]
    public async Task BidOnAMixedListingGoesToTheBasketBid()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        MixedBidDraft? captured = null;
        repository.Setup(r => r.GetListingAsync(Listing, It.IsAny<CancellationToken>())).ReturnsAsync(Detail());
        repository.Setup(r => r.PlaceMixedBidAsync(It.IsAny<MixedBidDraft>(), It.IsAny<CancellationToken>()))
            .Callback<MixedBidDraft, CancellationToken>((d, _) => captured = d)
            .ReturnsAsync(new AuctionBidResult(AuctionBidOutcome.Placed));
        var limits = new PriceLimits { MaximumComponents = 3 };
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = true, MixedPricesEnabled = false, PriceLimits = limits });
        var bid = Basket((PriceKind.Soul, 4), (PriceKind.Zen, 50_000));

        var placed = await service.BidAsync(new AuctionBidRequest(Guid.NewGuid(), Account, Character, Listing, 3, 0, "127.0.0.1", BidBasket: bid));
        var missing = await service.BidAsync(new AuctionBidRequest(Guid.NewGuid(), Account, Character, Listing, 3, 0, "127.0.0.1"));

        Assert.Multiple(() =>
        {
            Assert.That(placed.Result, Is.EqualTo(AuctionResultCode.Success));
            Assert.That(captured!.Value.Bid, Is.EqualTo(bid));
            Assert.That(captured.Value.ExpectedVersion, Is.EqualTo(3));
            Assert.That(captured.Value.Limits, Is.SameAs(limits));
            Assert.That(missing.Result, Is.EqualTo(AuctionResultCode.InvalidRequest));
        });
        repository.Verify(r => r.PlaceScalarBidAsync(It.IsAny<ScalarBidDraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies a bid on a buy-now listing is reported as an invalid request.
    /// </summary>
    [Test]
    public async Task BidOnABuyNowListingIsAnInvalidRequest()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        repository.Setup(r => r.GetListingAsync(Listing, It.IsAny<CancellationToken>())).ReturnsAsync(Detail());
        repository.Setup(r => r.PlaceMixedBidAsync(It.IsAny<MixedBidDraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuctionBidResult(AuctionBidOutcome.NotBiddable));
        var service = this.Service(repository);

        var result = await service.BidAsync(new AuctionBidRequest(Guid.NewGuid(), Account, Character, Listing, 3, 0, null, BidBasket: Basket((PriceKind.Soul, 4))));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.InvalidRequest));
    }

    /// <summary>
    /// Verifies a buyout of a mixed listing goes to the basket buyout.
    /// </summary>
    [Test]
    public async Task BuyoutOfAMixedListingGoesToTheBasketBuyout()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        MixedBuyoutDraft? captured = null;
        repository.Setup(r => r.GetListingAsync(Listing, It.IsAny<CancellationToken>())).ReturnsAsync(Detail());
        repository.Setup(r => r.PlaceMixedBuyoutAsync(It.IsAny<MixedBuyoutDraft>(), It.IsAny<CancellationToken>()))
            .Callback<MixedBuyoutDraft, CancellationToken>((d, _) => captured = d)
            .ReturnsAsync(new AuctionBuyoutResult(AuctionBuyoutOutcome.Bought));
        var service = this.Service(repository);

        var result = await service.BuyoutAsync(new AuctionBuyoutRequest(Guid.NewGuid(), Account, Character, Listing, 3, "127.0.0.1"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
            Assert.That(captured!.Value.ListingId, Is.EqualTo(Listing));
            Assert.That(captured.Value.ExpectedVersion, Is.EqualTo(3));
        });
        repository.Verify(r => r.BuyoutAsync(It.IsAny<BuyoutDraft>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PriceBasket Basket(params (PriceKind Kind, long Quantity)[] parts)
        => PriceBasket.FromComponents(parts.Select(p => new PriceComponent(p.Kind, p.Quantity)));

    private static AuctionListItemRequest MixedRequest(PriceBasket? start, PriceBasket? buyout)
        => new(Guid.NewGuid(), Account, Character, "Seller", "127.0.0.1", 20, (byte)AuctionCurrencyMode.Mixed, 0, 0, null, 24, StartingBasket: start, BuyoutBasket: buyout);

    private static AuctionListingDetail Detail()
        => new(
            Listing, 3, 0, (byte)AuctionCurrencyMode.Mixed, 0, Guid.NewGuid(), 0, null, null, DateTimeOffset.UtcNow.AddHours(5), DateTimeOffset.UtcNow.AddHours(5), 0, "Seller", null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            Basket((PriceKind.Soul, 3)).ToVectorText(), null, null);

    private AuctionHouseService Service(Mock<IAuctionHouseRepository> repository)
        => new(repository.Object, new AuctionHouseOptions { Enabled = true, MixedPricesEnabled = true });
}
