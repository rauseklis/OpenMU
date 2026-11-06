// <copyright file="AuctionFruitBidTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using Npgsql;

/// <summary>
/// Verifies fruit-basket bidding against PostgreSQL: the first bid must meet the starting basket, a later bid must raise every
/// nonzero component, and an outbid refunds the previous bidder's fruits as a fruit collection.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionFruitBidTests
{
    private static readonly byte StrengthFruit = AuctionBankWallet.FruitCode(FruitComponent.Strength);
    private static readonly byte EnergyFruit = AuctionBankWallet.FruitCode(FruitComponent.Energy);
    private string _connectionString = string.Empty;
    private Guid _sellerAccountId;
    private Guid _firstAccountId;
    private Guid _firstCharacterId;
    private Guid _secondAccountId;
    private Guid _secondCharacterId;
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
        var owners = await AuctionTestCharacters.FundOwnedAsync(this._connectionString, 0, 3);
        if (owners.Count < 3)
        {
            Assert.Ignore("The database has too few linked characters; use the populated copy.");
        }

        this._sellerAccountId = owners[0].AccountId;
        this._firstAccountId = owners[1].AccountId;
        this._firstCharacterId = owners[1].CharacterId;
        this._secondAccountId = owners[2].AccountId;
        this._secondCharacterId = owners[2].CharacterId;

        await using var context = new EntityDataContext();
        await AuctionTestBank.ClearAsync(this._connectionString, this._firstAccountId, this._secondAccountId, this._sellerAccountId);

        // Fruits are paid from the Jewel Bank: level 3 is the Strength fruit, level 0 the Energy fruit.
        await AuctionTestBank.AddAsync(this._connectionString, this._firstAccountId, this._firstCharacterId, StrengthFruit, 2);
        await AuctionTestBank.AddAsync(this._connectionString, this._firstAccountId, this._firstCharacterId, EnergyFruit, 1);
        await AuctionTestBank.AddAsync(this._connectionString, this._secondAccountId, this._secondCharacterId, StrengthFruit, 3);
        await AuctionTestBank.AddAsync(this._connectionString, this._secondAccountId, this._secondCharacterId, EnergyFruit, 2);

        var now = DateTimeOffset.UtcNow;
        this._listingId = Guid.NewGuid();
        context.Set<AuctionListing>().Add(new AuctionListing
        {
            Id = this._listingId,
            ItemId = Guid.NewGuid(),
            SellerAccountId = this._sellerAccountId,
            SellerCharacterId = Guid.NewGuid(),
            SellerName = "Seller",
            SellerIp = "127.0.0.1",
            Status = 0,
            Currency = 8,
            Category = 0,
            StartingPrice = 0,
            StartingStrength = 2,
            StartingAgility = 0,
            StartingVitality = 0,
            StartingEnergy = 1,
            StartingCommand = 0,
            CreatedAt = now,
            OriginalEndsAt = now.AddHours(24),
            EndsAt = now.AddHours(24),
            NormalizedName = string.Empty,
            Version = 0,
        });
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        var accounts = new[] { this._sellerAccountId, this._firstAccountId, this._secondAccountId };
        await context.Set<AuctionCurrencyMovement>().Where(m => accounts.Contains(m.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => accounts.Contains(c.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionNotification>().Where(n => accounts.Contains(n.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => b.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => accounts.Contains(o.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.Id == this._listingId).ExecuteDeleteAsync();
        await AuctionTestBank.ClearAsync(this._connectionString, accounts);
    }

    /// <summary>
    /// Verifies a first bid at the starting basket is placed, and its fruits leave the bidder's Jewel Bank.
    /// </summary>
    [Test]
    public async Task FirstBidAtTheStartingBasketIsPlaced()
    {
        var result = await PlaceAsync(this._firstAccountId, this._firstCharacterId, version: 0, new FruitBasket(2, 0, 0, 1, 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));
        Assert.Multiple(async () =>
        {
            Assert.That(await AuctionTestBank.CountAsync(this._firstAccountId, StrengthFruit), Is.EqualTo(0));
            Assert.That(await AuctionTestBank.CountAsync(this._firstAccountId, EnergyFruit), Is.EqualTo(0));
        });
    }

    /// <summary>
    /// Verifies a later bid which does not raise every nonzero component is refused, and no fruit is taken.
    /// </summary>
    [Test]
    public async Task BidThatDoesNotRaiseEveryComponentIsRefused()
    {
        await PlaceAsync(this._firstAccountId, this._firstCharacterId, version: 0, new FruitBasket(2, 0, 0, 1, 0));

        var result = await PlaceAsync(this._secondAccountId, this._secondCharacterId, version: 1, new FruitBasket(3, 0, 0, 1, 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.BidTooLow));
        Assert.Multiple(async () =>
        {
            Assert.That(await AuctionTestBank.CountAsync(this._secondAccountId, StrengthFruit), Is.EqualTo(3));
            Assert.That(await AuctionTestBank.CountAsync(this._secondAccountId, EnergyFruit), Is.EqualTo(2));
        });
    }

    /// <summary>
    /// Verifies a valid outbid refunds the displaced bid's fruits to its bidder as a fruit collection with each component.
    /// </summary>
    [Test]
    public async Task OutbidRefundsThePreviousBidAsAFruitCollection()
    {
        await PlaceAsync(this._firstAccountId, this._firstCharacterId, version: 0, new FruitBasket(2, 0, 0, 1, 0));

        var result = await PlaceAsync(this._secondAccountId, this._secondCharacterId, version: 1, new FruitBasket(3, 0, 0, 2, 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));
        await using var context = new EntityDataContext();
        var refund = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._firstAccountId && c.Kind == 3);
        Assert.Multiple(() =>
        {
            Assert.That(refund.Currency, Is.EqualTo(8));
            Assert.That(refund.OriginalStrength, Is.EqualTo(2));
            Assert.That(refund.OriginalEnergy, Is.EqualTo(1));
        });
    }

    private async Task<AuctionBidResult> PlaceAsync(Guid accountId, Guid characterId, int version, FruitBasket bid)
    {
        return await new AuctionHouseRepository().PlaceFruitBidAsync(new EntityDataContext(), new FruitBidDraft(
            OperationId: Guid.NewGuid(),
            BidderAccountId: accountId,
            BidderCharacterId: characterId,
            ListingId: this._listingId,
            ExpectedVersion: version,
            Bid: bid,
            BidderIp: "127.0.0.1",
            Now: DateTimeOffset.UtcNow,
            NewEndsAt: DateTimeOffset.UtcNow.AddHours(24)));
    }

    /// <summary>
    /// Verifies a fruit listing's browse row carries its starting basket before a bid, and its bid basket after one.
    /// </summary>
    [Test]
    public async Task BrowseRowCarriesTheFruitBaskets()
    {
        var before = await new AuctionHouseRepository().SearchListingsAsync(new EntityDataContext(), new AuctionListingSearch(Currency: 8, PageSize: 1000));
        var row = before.Rows.Single(r => r.ListingId == this._listingId);
        Assert.That(row.StartingFruits, Is.EqualTo(new FruitBasket(2, 0, 0, 1, 0)));
        Assert.That(row.CurrentFruits, Is.Null);

        await this.PlaceAsync(this._firstAccountId, this._firstCharacterId, version: 0, new FruitBasket(2, 0, 0, 1, 0));

        var after = await new AuctionHouseRepository().SearchListingsAsync(new EntityDataContext(), new AuctionListingSearch(Currency: 8, PageSize: 1000));
        Assert.That(after.Rows.Single(r => r.ListingId == this._listingId).CurrentFruits, Is.EqualTo(new FruitBasket(2, 0, 0, 1, 0)));
    }
}

