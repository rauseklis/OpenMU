// <copyright file="AuctionMixedTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using Npgsql;

/// <summary>
/// Verifies mixed-currency listings against PostgreSQL: a bid or buyout pays every component of its basket or none, a bid must
/// keep every currency of the current bid and raise one, the displaced bid is refunded as a basket, the sale pays the seller a
/// basket, and a basket collection is claimed all at once. Requires <c>AUCTION_TEST_CONNECTION_STRING</c>.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionMixedTests
{
    private const long SaleFee = 100_000;
    private const int StartingMoney = 1_000_000;
    private readonly List<Guid> _listings = new();
    private string _connectionString = string.Empty;
    private Guid _sellerAccount;
    private Guid _bidderA;
    private Guid _characterA;
    private Guid _inventoryA;
    private Guid _bidderB;
    private Guid _characterB;
    private Guid _inventoryB;
    private Guid _soul;
    private Guid _bless;
    private Guid _fruit;
    private Guid _other;

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
        var owners = await AuctionTestCharacters.FundOwnedAsync(this._connectionString, StartingMoney, 3);
        if (owners.Count < 3)
        {
            Assert.Ignore("The database has too few linked characters; use the populated copy.");
        }

        this._sellerAccount = owners[0].AccountId;
        (this._bidderA, this._characterA) = owners[1];
        (this._bidderB, this._characterB) = owners[2];
        this._soul = await this.DefinitionAsync(14, 14);
        this._bless = await this.DefinitionAsync(14, 13);
        this._fruit = await this.DefinitionAsync(13, 15);
        await using var context = new EntityDataContext();
        this._other = await context.Set<Item>().Where(i => i.DefinitionId != null && i.DefinitionId != this._soul && i.DefinitionId != this._bless && i.DefinitionId != this._fruit)
            .Select(i => i.DefinitionId!.Value).FirstAsync();
        this._inventoryA = (await context.Set<Character>().Where(c => c.Id == this._characterA).Select(c => c.InventoryId).SingleAsync())!.Value;
        this._inventoryB = (await context.Set<Character>().Where(c => c.Id == this._characterB).Select(c => c.InventoryId).SingleAsync())!.Value;
        await this.ClearInventoriesAsync(context);
        await AddAsync(context, this._inventoryA, this._soul, 0, 5, 70);
        await AddAsync(context, this._inventoryA, this._bless, 0, 2, 75);
        await AddAsync(context, this._inventoryA, this._fruit, FruitRules.GetItemLevel(FruitComponent.Strength), 2, 77);
        await AddAsync(context, this._inventoryB, this._soul, 0, 6, 70);
        await AddAsync(context, this._inventoryB, this._bless, 0, 2, 76);
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        var accounts = new[] { this._sellerAccount, this._bidderA, this._bidderB };
        await context.Set<AuctionCurrencyMovement>().Where(m => accounts.Contains(m.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => accounts.Contains(c.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionNotification>().Where(n => accounts.Contains(n.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => this._listings.Contains(b.ListingId)).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => accounts.Contains(o.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => this._listings.Contains(l.Id) || l.SellerAccountId == this._sellerAccount).ExecuteDeleteAsync();
        this._listings.Clear();
        await this.ClearInventoriesAsync(context);
    }

    /// <summary>
    /// Verifies a bid takes every component of its basket and records it as the high bid.
    /// </summary>
    [Test]
    public async Task PlacedBidEscrowsEveryComponent()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));

        var result = await new AuctionHouseRepository().PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderA, this._characterA, listing, 0, (PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));
        await using var context = new EntityDataContext();
        var stored = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == listing);
        var bid = await context.Set<AuctionBid>().AsNoTracking().SingleAsync(b => b.ListingId == listing && b.Status == 0);
        Assert.Multiple(async () =>
        {
            Assert.That(await this.CountAsync(this._inventoryA, this._soul), Is.EqualTo(2));
            Assert.That(await this.MoneyAsync(this._inventoryA), Is.EqualTo(StartingMoney - 50_000));
            Assert.That(stored.CurrentBidVector, Is.EqualTo("50000,0,0,3,0,0,0,0,0,0,0,0,0"));
            Assert.That(stored.BidCount, Is.EqualTo(1));
            Assert.That(stored.CurrentBidderAccountId, Is.EqualTo(this._bidderA));
            Assert.That(bid.AmountVector, Is.EqualTo(stored.CurrentBidVector));
            Assert.That(bid.Currency, Is.EqualTo((byte)AuctionCurrencyMode.Mixed));
        });
    }

    /// <summary>
    /// Verifies the first bid must reach the starting price in every currency of that price.
    /// </summary>
    [Test]
    public async Task BidBelowTheStartingPriceInAnyCurrencyIsTooLow()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));

        var result = await new AuctionHouseRepository().PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderA, this._characterA, listing, 0, (PriceKind.Soul, 5)));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.BidTooLow));
        Assert.That(await this.CountAsync(this._inventoryA, this._soul), Is.EqualTo(5));
    }

    /// <summary>
    /// Verifies a bid which cannot pay every component takes nothing: the components paid first are given back.
    /// </summary>
    [Test]
    public async Task BidThatCannotPayEveryComponentChangesNothing()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));
        await this.SetMoneyAsync(this._inventoryA, 10_000);

        var result = await new AuctionHouseRepository().PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderA, this._characterA, listing, 0, (PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.InsufficientCurrency));
        await using var context = new EntityDataContext();
        var stored = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == listing);
        Assert.Multiple(async () =>
        {
            Assert.That(await this.CountAsync(this._inventoryA, this._soul), Is.EqualTo(5));
            Assert.That(await this.MoneyAsync(this._inventoryA), Is.EqualTo(10_000));
            Assert.That(stored.BidCount, Is.Zero);
            Assert.That(stored.CurrentBidVector, Is.Null);
        });
    }

    /// <summary>
    /// Verifies a raise must keep every currency of the current bid, may add currencies, and refunds the displaced bidder a basket.
    /// </summary>
    [Test]
    public async Task RaiseKeepsEveryCurrencyAndRefundsTheDisplacedBid()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));
        var repository = new AuctionHouseRepository();
        Assert.That((await repository.PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderA, this._characterA, listing, 0, (PriceKind.Soul, 3), (PriceKind.Zen, 50_000)))).Outcome, Is.EqualTo(AuctionBidOutcome.Placed));

        var dropsZen = await repository.PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderB, this._characterB, listing, 1, (PriceKind.Soul, 4)));
        var noRaise = await repository.PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderB, this._characterB, listing, 1, (PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));
        var raise = await repository.PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderB, this._characterB, listing, 1, (PriceKind.Soul, 3), (PriceKind.Zen, 50_000), (PriceKind.Bless, 1)));

        Assert.Multiple(() =>
        {
            Assert.That(dropsZen.Outcome, Is.EqualTo(AuctionBidOutcome.BidTooLow));
            Assert.That(noRaise.Outcome, Is.EqualTo(AuctionBidOutcome.BidTooLow));
            Assert.That(raise.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));
        });
        await using var context = new EntityDataContext();
        var refund = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._bidderA && c.Kind == 3);
        var first = await context.Set<AuctionBid>().AsNoTracking().SingleAsync(b => b.ListingId == listing && b.BidderAccountId == this._bidderA);
        Assert.Multiple(async () =>
        {
            Assert.That(refund.Currency, Is.EqualTo((byte)AuctionCurrencyMode.Mixed));
            Assert.That(refund.OriginalVector, Is.EqualTo("50000,0,0,3,0,0,0,0,0,0,0,0,0"));
            Assert.That(refund.RemainingVector, Is.EqualTo(refund.OriginalVector));
            Assert.That(first.Status, Is.EqualTo(1));
            Assert.That(await this.CountAsync(this._inventoryB, this._soul), Is.EqualTo(3));
            Assert.That(await this.CountAsync(this._inventoryB, this._bless), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies the seller cannot bid on their own listing.
    /// </summary>
    [Test]
    public async Task SellerCannotBidOnTheirOwnListing()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3)));
        var draft = this.Bid(this._sellerAccount, this._characterA, listing, 0, (PriceKind.Soul, 3));

        var result = await new AuctionHouseRepository().PlaceMixedBidAsync(new EntityDataContext(), draft);

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.SelfBid));
    }

    /// <summary>
    /// Verifies a buy-now listing, which has no starting price, takes no bids.
    /// </summary>
    [Test]
    public async Task BuyNowListingTakesNoBids()
    {
        var listing = await this.InsertAsync(start: null, buyout: Basket((PriceKind.Soul, 4)));

        var result = await new AuctionHouseRepository().PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderA, this._characterA, listing, 0, (PriceKind.Soul, 4)));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.NotBiddable));
    }

    /// <summary>
    /// Verifies a buyout takes the whole buyout basket, sells the listing, consumes the held fee and pays the seller a basket.
    /// </summary>
    [Test]
    public async Task BuyoutPaysTheBasketAndCreatesTheCollections()
    {
        var listing = await this.InsertAsync(start: null, buyout: Basket((PriceKind.Soul, 4), (PriceKind.Bless, 1), (PriceKind.Zen, 20_000)));

        var result = await new AuctionHouseRepository().PlaceMixedBuyoutAsync(new EntityDataContext(), this.Buyout(this._bidderA, this._characterA, listing, 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        await using var context = new EntityDataContext();
        var stored = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == listing);
        var proceeds = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._sellerAccount && c.Kind == 2);
        var item = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._bidderA && c.Kind == 0);
        Assert.Multiple(async () =>
        {
            Assert.That(await this.CountAsync(this._inventoryA, this._soul), Is.EqualTo(1));
            Assert.That(await this.CountAsync(this._inventoryA, this._bless), Is.EqualTo(1));
            Assert.That(await this.MoneyAsync(this._inventoryA), Is.EqualTo(StartingMoney - 20_000));
            Assert.That(stored.Status, Is.EqualTo(1));
            Assert.That(stored.SaleFeeState, Is.EqualTo((byte)SaleFeeState.Consumed));
            Assert.That(proceeds.Currency, Is.EqualTo((byte)AuctionCurrencyMode.Mixed));
            Assert.That(proceeds.OriginalVector, Is.EqualTo("20000,0,1,4,0,0,0,0,0,0,0,0,0"));
            Assert.That(item.ItemId, Is.EqualTo(stored.ItemId));
        });
    }

    /// <summary>
    /// Verifies a buyout which cannot pay every component takes nothing and leaves the listing active.
    /// </summary>
    [Test]
    public async Task BuyoutThatCannotPayEveryComponentChangesNothing()
    {
        var listing = await this.InsertAsync(start: null, buyout: Basket((PriceKind.Soul, 4), (PriceKind.Bless, 3)));

        var result = await new AuctionHouseRepository().PlaceMixedBuyoutAsync(new EntityDataContext(), this.Buyout(this._bidderA, this._characterA, listing, 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.InsufficientCurrency));
        await using var context = new EntityDataContext();
        Assert.Multiple(async () =>
        {
            Assert.That(await this.CountAsync(this._inventoryA, this._soul), Is.EqualTo(5));
            Assert.That((await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == listing)).Status, Is.Zero);
        });
    }

    /// <summary>
    /// Verifies the high bidder who buys out pays only the part of the buyout they have not escrowed.
    /// </summary>
    [Test]
    public async Task BuyoutAfterOwnBidPaysOnlyTheDifference()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.Zen, 50_000)), Basket((PriceKind.Soul, 4), (PriceKind.Zen, 50_000), (PriceKind.Bless, 1)));
        var repository = new AuctionHouseRepository();
        Assert.That((await repository.PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderA, this._characterA, listing, 0, (PriceKind.Soul, 3), (PriceKind.Zen, 50_000)))).Outcome, Is.EqualTo(AuctionBidOutcome.Placed));

        var result = await repository.PlaceMixedBuyoutAsync(new EntityDataContext(), this.Buyout(this._bidderA, this._characterA, listing, 1));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        await using var context = new EntityDataContext();
        Assert.Multiple(async () =>
        {
            Assert.That(await this.CountAsync(this._inventoryA, this._soul), Is.EqualTo(1));
            Assert.That(await this.CountAsync(this._inventoryA, this._bless), Is.EqualTo(1));
            Assert.That(await this.MoneyAsync(this._inventoryA), Is.EqualTo(StartingMoney - 50_000));
            Assert.That(await context.Set<AuctionCollection>().AsNoTracking().CountAsync(c => c.AccountId == this._bidderA && c.Kind == 3), Is.Zero);
        });
    }

    /// <summary>
    /// Verifies a buyout by another account refunds the displaced bidder their basket.
    /// </summary>
    [Test]
    public async Task BuyoutAfterAnotherBiddersBidRefundsIt()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3)), Basket((PriceKind.Soul, 5)));
        var repository = new AuctionHouseRepository();
        Assert.That((await repository.PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderA, this._characterA, listing, 0, (PriceKind.Soul, 3)))).Outcome, Is.EqualTo(AuctionBidOutcome.Placed));

        var result = await repository.PlaceMixedBuyoutAsync(new EntityDataContext(), this.Buyout(this._bidderB, this._characterB, listing, 1));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        await using var context = new EntityDataContext();
        var refund = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._bidderA && c.Kind == 3);
        Assert.That(refund.OriginalVector, Is.EqualTo("0,0,0,3,0,0,0,0,0,0,0,0,0"));
    }

    /// <summary>
    /// Verifies a listing without a buyout refuses buyouts.
    /// </summary>
    [Test]
    public async Task ListingWithoutABuyoutRefusesBuyouts()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3)));

        var result = await new AuctionHouseRepository().PlaceMixedBuyoutAsync(new EntityDataContext(), this.Buyout(this._bidderA, this._characterA, listing, 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.BuyoutUnavailable));
    }

    /// <summary>
    /// Verifies an expired listing with a bid sells to the high bidder and pays the seller the bid basket.
    /// </summary>
    [Test]
    public async Task ExpiryWithABidSellsAndPaysTheSellerABasket()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));
        var repository = new AuctionHouseRepository();
        var bid = this.Bid(this._bidderA, this._characterA, listing, 0, (PriceKind.Soul, 3), (PriceKind.Zen, 50_000)) with { NewEndsAt = DateTimeOffset.UtcNow.AddHours(1) };
        Assert.That((await repository.PlaceMixedBidAsync(new EntityDataContext(), bid)).Outcome, Is.EqualTo(AuctionBidOutcome.Placed));

        await repository.ExpireBatchAsync(new EntityDataContext(), DateTimeOffset.UtcNow.AddHours(2), 100, _ => 0);

        await using var context = new EntityDataContext();
        var stored = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == listing);
        var proceeds = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._sellerAccount && c.Kind == 2);
        Assert.Multiple(async () =>
        {
            Assert.That(stored.Status, Is.EqualTo(1));
            Assert.That(stored.SaleFeeState, Is.EqualTo((byte)SaleFeeState.Consumed));
            Assert.That(proceeds.OriginalVector, Is.EqualTo("50000,0,0,3,0,0,0,0,0,0,0,0,0"));
            Assert.That(await context.Set<AuctionCollection>().AsNoTracking().CountAsync(c => c.AccountId == this._bidderA && c.Kind == 0), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies an administrative removal returns the escrowed basket to the high bidder and refunds the sale fee.
    /// </summary>
    [Test]
    public async Task AdminRemovalRefundsTheMixedEscrowAndTheFee()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));
        var repository = new AuctionHouseRepository();
        Assert.That((await repository.PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderA, this._characterA, listing, 0, (PriceKind.Soul, 3), (PriceKind.Zen, 50_000)))).Outcome, Is.EqualTo(AuctionBidOutcome.Placed));

        var removed = await repository.AdminRemoveAsync(new EntityDataContext(), new AdminRemoveDraft(listing, "tester", "test", DateTimeOffset.UtcNow));

        Assert.That(removed, Is.EqualTo(AdminRemoveOutcome.Removed));
        await using var context = new EntityDataContext();
        var refund = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._bidderA && c.Kind == 3);
        var fee = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._sellerAccount && c.Kind == 6);
        Assert.Multiple(() =>
        {
            Assert.That(refund.OriginalVector, Is.EqualTo("50000,0,0,3,0,0,0,0,0,0,0,0,0"));
            Assert.That(fee.RemainingScalar, Is.EqualTo(SaleFee));
        });
    }

    /// <summary>
    /// Verifies claiming a basket collection credits the Zen and creates the jewels and fruits, all at once.
    /// </summary>
    [Test]
    public async Task ClaimingABasketPaysEveryComponentAtOnce()
    {
        var collection = await this.InsertCollectionAsync(Basket((PriceKind.Zen, 20_000), (PriceKind.Soul, 3), (PriceKind.Bless, 1), (PriceKind.StrengthFruit, 2)));
        var repository = new AuctionHouseRepository();

        var claimed = await repository.ClaimAsync(new EntityDataContext(), new ClaimDraft(Guid.NewGuid(), this._bidderB, this._characterB, collection.Id, 0, DateTimeOffset.UtcNow));
        var again = await repository.ClaimAsync(new EntityDataContext(), new ClaimDraft(Guid.NewGuid(), this._bidderB, this._characterB, collection.Id, 1, DateTimeOffset.UtcNow));

        await using var context = new EntityDataContext();
        var stored = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.Id == collection.Id);
        Assert.Multiple(async () =>
        {
            Assert.That(claimed.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
            Assert.That(again.Outcome, Is.EqualTo(AuctionClaimOutcome.AlreadyClaimed));
            Assert.That(await this.MoneyAsync(this._inventoryB), Is.EqualTo(StartingMoney + 20_000));
            Assert.That(await this.CountAsync(this._inventoryB, this._soul), Is.EqualTo(6 + 3));
            Assert.That(await this.CountAsync(this._inventoryB, this._bless), Is.EqualTo(2 + 1));
            Assert.That(await this.CountAsync(this._inventoryB, this._fruit), Is.EqualTo(2));
            Assert.That(stored.Status, Is.EqualTo(2));
            Assert.That(stored.RemainingVector, Is.EqualTo("0,0,0,0,0,0,0,0,0,0,0,0,0"));
        });
    }

    /// <summary>
    /// Verifies a basket claim which cannot credit the Zen changes nothing: the jewels are not created either.
    /// </summary>
    [Test]
    public async Task ClaimingABasketThatCannotCreditZenChangesNothing()
    {
        var collection = await this.InsertCollectionAsync(Basket((PriceKind.Zen, 20_000), (PriceKind.Soul, 3)));
        await this.SetMoneyAsync(this._inventoryB, AuctionHouseRepository.DefaultMoneyLimit - 10);

        var claimed = await new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(Guid.NewGuid(), this._bidderB, this._characterB, collection.Id, 0, DateTimeOffset.UtcNow));

        await using var context = new EntityDataContext();
        var stored = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.Id == collection.Id);
        Assert.Multiple(async () =>
        {
            Assert.That(claimed.Outcome, Is.EqualTo(AuctionClaimOutcome.MoneyLimit));
            Assert.That(await this.CountAsync(this._inventoryB, this._soul), Is.EqualTo(6));
            Assert.That(stored.Status, Is.Zero);
            Assert.That(stored.Version, Is.Zero);
        });
    }

    /// <summary>
    /// Verifies a basket claim which has no room for its items changes nothing: the Zen is not credited either.
    /// </summary>
    [Test]
    public async Task ClaimingABasketWithoutRoomChangesNothing()
    {
        var collection = await this.InsertCollectionAsync(Basket((PriceKind.Zen, 20_000), (PriceKind.Soul, 3)));
        await using (var context = new EntityDataContext())
        {
            var used = await context.Set<Item>().AsNoTracking().Where(i => i.ItemStorageId == this._inventoryB).Select(i => (int)i.ItemSlot).ToListAsync();
            foreach (var slot in Enumerable.Range(InventoryConstants.EquippableSlotsCount, 64).Where(s => !used.Contains(s)))
            {
                context.Set<Item>().Add(new Item { Id = Guid.NewGuid(), ItemStorageId = this._inventoryB, ItemSlot = (byte)slot, DefinitionId = this._other, Durability = 1 });
            }

            await context.SaveChangesAsync();
        }

        var claimed = await new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(Guid.NewGuid(), this._bidderB, this._characterB, collection.Id, 0, DateTimeOffset.UtcNow));

        await using var check = new EntityDataContext();
        var stored = await check.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.Id == collection.Id);
        Assert.Multiple(async () =>
        {
            Assert.That(claimed.Outcome, Is.EqualTo(AuctionClaimOutcome.InventoryFull));
            Assert.That(await this.MoneyAsync(this._inventoryB), Is.EqualTo(StartingMoney));
            Assert.That(stored.Status, Is.Zero);
        });
    }

    /// <summary>
    /// Verifies a partial claim of a basket is refused: a basket is claimed whole.
    /// </summary>
    [Test]
    public async Task ClaimingPartOfABasketIsRefused()
    {
        var collection = await this.InsertCollectionAsync(Basket((PriceKind.Zen, 20_000), (PriceKind.Soul, 3)));

        var claimed = await new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(Guid.NewGuid(), this._bidderB, this._characterB, collection.Id, 0, DateTimeOffset.UtcNow, RequestedScalar: 1));

        Assert.That(claimed.Outcome, Is.EqualTo(AuctionClaimOutcome.InvalidClaimQuantity));
    }

    /// <summary>
    /// Verifies the currency filter matches a mixed listing which uses that kind, and rows carry their baskets.
    /// </summary>
    [Test]
    public async Task CurrencyFilterMatchesMixedListingsByKind()
    {
        var soulAndZen = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));
        var blessAndZen = await this.InsertAsync(Basket((PriceKind.Bless, 2), (PriceKind.Zen, 50_000)));
        var repository = new AuctionHouseRepository();

        var soul = await repository.SearchListingsAsync(new EntityDataContext(), new AuctionListingSearch(Currency: 3, PageSize: 50));
        var zen = await repository.SearchListingsAsync(new EntityDataContext(), new AuctionListingSearch(Currency: 0, PageSize: 50));
        var mixed = await repository.SearchListingsAsync(new EntityDataContext(), new AuctionListingSearch(Currency: (byte)AuctionCurrencyMode.Mixed, PageSize: 50));
        var priced = await repository.SearchListingsAsync(new EntityDataContext(), new AuctionListingSearch(Currency: 3, Sort: AuctionSearchSort.PriceAscending, PageSize: 50));

        Assert.Multiple(() =>
        {
            Assert.That(soul.Rows.Select(r => r.ListingId), Does.Contain(soulAndZen));
            Assert.That(soul.Rows.Select(r => r.ListingId), Does.Not.Contain(blessAndZen));
            Assert.That(zen.Rows.Select(r => r.ListingId), Does.Contain(soulAndZen).And.Contain(blessAndZen));
            Assert.That(mixed.Rows.Select(r => r.ListingId), Does.Contain(soulAndZen).And.Contain(blessAndZen));
            Assert.That(soul.Rows.Single(r => r.ListingId == soulAndZen).PriceVector, Is.EqualTo("50000,0,0,3,0,0,0,0,0,0,0,0,0"));
            Assert.That(priced.Rows.Select(r => r.ListingId), Does.Not.Contain(soulAndZen), "a price order does not rank baskets");
        });
    }

    /// <summary>
    /// Verifies the listing detail carries the baskets of a mixed listing.
    /// </summary>
    [Test]
    public async Task DetailCarriesTheBaskets()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3)), Basket((PriceKind.Soul, 5)));

        var detail = await new AuctionHouseRepository().GetListingAsync(new EntityDataContext(), listing);

        Assert.Multiple(() =>
        {
            Assert.That(detail!.Currency, Is.EqualTo((byte)AuctionCurrencyMode.Mixed));
            Assert.That(detail.PriceVector, Is.EqualTo("0,0,0,3,0,0,0,0,0,0,0,0,0"));
            Assert.That(detail.BuyoutVector, Is.EqualTo("0,0,0,5,0,0,0,0,0,0,0,0,0"));
            Assert.That(detail.CurrentBidVector, Is.Null);
        });
    }

    /// <summary>
    /// Verifies the mixed escrow is consistent with the listing, so the reconciliation reports nothing for it, and a drift is flagged.
    /// </summary>
    [Test]
    public async Task ReconciliationAcceptsAConsistentMixedListingAndFlagsADrift()
    {
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.Zen, 50_000)));
        var repository = new AuctionHouseRepository();
        Assert.That((await repository.PlaceMixedBidAsync(new EntityDataContext(), this.Bid(this._bidderA, this._characterA, listing, 0, (PriceKind.Soul, 3), (PriceKind.Zen, 50_000)))).Outcome, Is.EqualTo(AuctionBidOutcome.Placed));

        var consistent = await repository.ReconcileAsync(new EntityDataContext());

        await using (var context = new EntityDataContext())
        {
            await context.Set<AuctionListing>().Where(l => l.Id == listing).ExecuteUpdateAsync(s => s.SetProperty(l => l.CurrentBidVector, "1,0,0,3,0,0,0,0,0,0,0,0,0"));
        }

        var drifted = await repository.ReconcileAsync(new EntityDataContext());

        Assert.Multiple(() =>
        {
            Assert.That(consistent.Where(f => f.Subject == listing.ToString()), Is.Empty);
            Assert.That(drifted.Where(f => f.Subject == listing.ToString()).Select(f => f.Invariant), Does.Contain("MixedEscrowDiffersFromBidObligation"));
        });
    }

    private static PriceBasket Basket(params (PriceKind Kind, long Quantity)[] parts)
        => PriceBasket.FromComponents(parts.Select(p => new PriceComponent(p.Kind, p.Quantity)));

    private static async Task AddAsync(EntityDataContext context, Guid inventory, Guid definition, int level, int count, int startSlot)
    {
        for (var index = 0; index < count; index++)
        {
            context.Set<Item>().Add(new Item { Id = Guid.NewGuid(), ItemStorageId = inventory, ItemSlot = (byte)(startSlot + index), DefinitionId = definition, Level = (byte)level, Durability = 1 });
        }

        await context.SaveChangesAsync();
    }

    private MixedBidDraft Bid(Guid account, Guid character, Guid listing, int version, params (PriceKind Kind, long Quantity)[] parts)
        => new(Guid.NewGuid(), account, character, listing, version, Basket(parts), "127.0.0.1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(24));

    private MixedBuyoutDraft Buyout(Guid account, Guid character, Guid listing, int version)
        => new(Guid.NewGuid(), account, character, listing, version, "127.0.0.1", DateTimeOffset.UtcNow);

    private Task<Guid> InsertAsync(PriceBasket start) => this.InsertAsync(start, null);

    private async Task<Guid> InsertAsync(PriceBasket? start, PriceBasket? buyout)
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        this._listings.Add(id);
        await using var context = new EntityDataContext();
        context.Set<AuctionListing>().Add(new AuctionListing
        {
            Id = id,
            ItemId = Guid.NewGuid(),
            SellerAccountId = this._sellerAccount,
            SellerCharacterId = Guid.NewGuid(),
            SellerName = "Seller",
            SellerIp = "10.0.0.1",
            Status = 0,
            Currency = (byte)AuctionCurrencyMode.Mixed,
            Category = 0,
            SubCategory = 255,
            PriceVector = start?.ToVectorText(),
            BuyoutVector = buyout?.ToVectorText(),
            KindMask = (start ?? PriceBasket.Empty).KindMask | (buyout ?? PriceBasket.Empty).KindMask,
            SaleFeeHeld = SaleFee,
            SaleFeeState = (byte)SaleFeeState.Held,
            CreatedAt = now,
            OriginalEndsAt = now.AddHours(24),
            EndsAt = now.AddHours(24),
            NormalizedName = string.Empty,
            Version = 0,
        });
        await context.SaveChangesAsync();
        return id;
    }

    private async Task<AuctionCollection> InsertCollectionAsync(PriceBasket basket)
    {
        var now = DateTimeOffset.UtcNow;
        var collection = new AuctionCollection
        {
            Id = Guid.NewGuid(),
            AccountId = this._bidderB,
            Kind = 2,
            Currency = (byte)AuctionCurrencyMode.Mixed,
            OriginalVector = basket.ToVectorText(),
            RemainingVector = basket.ToVectorText(),
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };
        await using var context = new EntityDataContext();
        context.Set<AuctionCollection>().Add(collection);
        await context.SaveChangesAsync();
        return collection;
    }

    private async Task ClearInventoriesAsync(EntityDataContext context)
    {
        var inventories = new[] { this._inventoryA, this._inventoryB };
        var definitions = new[] { this._soul, this._bless, this._fruit, this._other };
        await context.Set<Item>().Where(i => inventories.Contains(i.ItemStorageId!.Value) && i.DefinitionId != null && definitions.Contains(i.DefinitionId.Value) && i.ItemSlot >= InventoryConstants.EquippableSlotsCount).ExecuteDeleteAsync();
    }

    private async Task<Guid> DefinitionAsync(int group, int number)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Id\" FROM config.\"ItemDefinition\" WHERE \"Group\" = @group AND \"Number\" = @number", connection);
        command.Parameters.AddWithValue("group", (short)group);
        command.Parameters.AddWithValue("number", (short)number);
        var found = await command.ExecuteScalarAsync();
        if (found is null)
        {
            Assert.Ignore($"The database has no item {group}/{number}; use the populated copy.");
        }

        return (Guid)found!;
    }

    private async Task<int> CountAsync(Guid inventory, Guid definition)
    {
        await using var context = new EntityDataContext();
        return await context.Set<Item>().AsNoTracking().CountAsync(i => i.ItemStorageId == inventory && i.DefinitionId == definition);
    }

    private async Task SetMoneyAsync(Guid inventory, int money)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE data.\"ItemStorage\" SET \"Money\" = @money WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("money", money);
        command.Parameters.AddWithValue("id", inventory);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> MoneyAsync(Guid inventory)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Money\" FROM data.\"ItemStorage\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", inventory);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
