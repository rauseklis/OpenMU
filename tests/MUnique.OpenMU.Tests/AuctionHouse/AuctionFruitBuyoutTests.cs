// <copyright file="AuctionFruitBuyoutTests.cs" company="MUnique">
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
/// Verifies fruit-basket buyouts against PostgreSQL: the buyer pays the buyout basket, the seller receives the proceeds after the
/// fee on each component, and a buyout after a bid refunds the bid's fruits.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionFruitBuyoutTests
{
    private string _connectionString = string.Empty;
    private Guid _sellerAccountId;
    private Guid _buyerAccountId;
    private Guid _buyerCharacterId;
    private Guid _buyerInventoryId;
    private Guid _bidderAccountId;
    private Guid _bidderCharacterId;
    private Guid _bidderInventoryId;
    private Guid _fruitDefinitionId;
    private Guid _listingId;
    private readonly List<Guid> _listings = new();

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
        this._buyerAccountId = owners[1].AccountId;
        this._buyerCharacterId = owners[1].CharacterId;
        this._bidderAccountId = owners[2].AccountId;
        this._bidderCharacterId = owners[2].CharacterId;
        this._fruitDefinitionId = await FruitDefinitionAsync(this._connectionString);

        await using var context = new EntityDataContext();
        this._buyerInventoryId = (await context.Set<Character>().AsNoTracking().Where(c => c.Id == this._buyerCharacterId).Select(c => c.InventoryId).SingleAsync())!.Value;
        this._bidderInventoryId = (await context.Set<Character>().AsNoTracking().Where(c => c.Id == this._bidderCharacterId).Select(c => c.InventoryId).SingleAsync())!.Value;
        await context.Set<Item>().Where(i => (i.ItemStorageId == this._buyerInventoryId || i.ItemStorageId == this._bidderInventoryId) && i.DefinitionId == this._fruitDefinitionId).ExecuteDeleteAsync();

        await AddFruitsAsync(context, this._buyerInventoryId, this._fruitDefinitionId, level: 3, count: 5, startSlot: 70);
        await AddFruitsAsync(context, this._buyerInventoryId, this._fruitDefinitionId, level: 0, count: 3, startSlot: 65);
        await AddFruitsAsync(context, this._bidderInventoryId, this._fruitDefinitionId, level: 3, count: 2, startSlot: 60);
        await AddFruitsAsync(context, this._bidderInventoryId, this._fruitDefinitionId, level: 0, count: 1, startSlot: 62);

        this._listingId = await this.InsertListingAsync(context, buyout: new FruitBasket(5, 0, 0, 3, 0));
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        var accounts = new[] { this._sellerAccountId, this._buyerAccountId, this._bidderAccountId };
        await context.Set<AuctionCurrencyMovement>().Where(m => accounts.Contains(m.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => accounts.Contains(c.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionNotification>().Where(n => accounts.Contains(n.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => this._listings.Contains(b.ListingId)).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => accounts.Contains(o.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => this._listings.Contains(l.Id)).ExecuteDeleteAsync();
        this._listings.Clear();
        await context.Set<Item>().Where(i => (i.ItemStorageId == this._buyerInventoryId || i.ItemStorageId == this._bidderInventoryId) && i.DefinitionId == this._fruitDefinitionId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies a buyout pays the buyout basket from the buyer's fruits, and the seller receives the basket less the fee on each component.
    /// </summary>
    [Test]
    public async Task BuyoutPaysTheBasketAndCreatesProceeds()
    {
        var result = await new AuctionHouseRepository().PlaceFruitBuyoutAsync(new EntityDataContext(), this.Buyout(this._listingId, version: 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<Item>().AsNoTracking().CountAsync(i => i.ItemStorageId == this._buyerInventoryId && i.DefinitionId == this._fruitDefinitionId), Is.EqualTo(0));
        var proceeds = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._sellerAccountId && c.Kind == 2);
        Assert.Multiple(() =>
        {
            Assert.That(proceeds.OriginalStrength, Is.EqualTo(4));
            Assert.That(proceeds.OriginalEnergy, Is.EqualTo(2));
        });
    }

    /// <summary>
    /// Verifies a buyout after a bid refunds the bid's fruits to its bidder, and the buyer pays the whole basket.
    /// </summary>
    [Test]
    public async Task BuyoutAfterABidRefundsTheBid()
    {
        var bid = await new AuctionHouseRepository().PlaceFruitBidAsync(new EntityDataContext(), new FruitBidDraft(
            OperationId: Guid.NewGuid(),
            BidderAccountId: this._bidderAccountId,
            BidderCharacterId: this._bidderCharacterId,
            ListingId: this._listingId,
            ExpectedVersion: 0,
            Bid: new FruitBasket(2, 0, 0, 1, 0),
            BidderIp: "127.0.0.1",
            Now: DateTimeOffset.UtcNow,
            NewEndsAt: DateTimeOffset.UtcNow.AddHours(24)));
        Assert.That(bid.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));

        var result = await new AuctionHouseRepository().PlaceFruitBuyoutAsync(new EntityDataContext(), this.Buyout(this._listingId, version: 1));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        await using var context = new EntityDataContext();
        var refund = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._bidderAccountId && c.Kind == 3);
        Assert.Multiple(() =>
        {
            Assert.That(refund.OriginalStrength, Is.EqualTo(2));
            Assert.That(refund.OriginalEnergy, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies a listing without a buyout basket refuses buyouts.
    /// </summary>
    [Test]
    public async Task ListingWithoutABuyoutBasketRefusesBuyouts()
    {
        await using var context = new EntityDataContext();
        var listing = await this.InsertListingAsync(context, buyout: null);

        var result = await new AuctionHouseRepository().PlaceFruitBuyoutAsync(new EntityDataContext(), this.Buyout(listing, version: 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.BuyoutUnavailable));
    }

    private FruitBuyoutDraft Buyout(Guid listingId, int version)
    {
        return new FruitBuyoutDraft(
            OperationId: Guid.NewGuid(),
            BuyerAccountId: this._buyerAccountId,
            BuyerCharacterId: this._buyerCharacterId,
            ListingId: listingId,
            ExpectedVersion: version,
            BuyerIp: "127.0.0.1",
            Now: DateTimeOffset.UtcNow,
            SuccessFeeBasisPoints: 500);
    }

    private async Task<Guid> InsertListingAsync(EntityDataContext context, FruitBasket? buyout)
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        this._listings.Add(id);
        context.Set<AuctionListing>().Add(new AuctionListing
        {
            Id = id,
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
            StartingEnergy = 1,
            BuyoutStrength = buyout?.Strength,
            BuyoutAgility = buyout?.Agility,
            BuyoutVitality = buyout?.Vitality,
            BuyoutEnergy = buyout?.Energy,
            BuyoutCommand = buyout?.Command,
            CreatedAt = now,
            OriginalEndsAt = now.AddHours(24),
            EndsAt = now.AddHours(24),
            NormalizedName = string.Empty,
            Version = 0,
        });
        await context.SaveChangesAsync();
        return id;
    }

    private static async Task AddFruitsAsync(EntityDataContext context, Guid inventoryId, Guid definitionId, byte level, int count, int startSlot)
    {
        for (var index = 0; index < count; index++)
        {
            context.Set<Item>().Add(new Item { Id = Guid.NewGuid(), ItemStorageId = inventoryId, ItemSlot = (byte)(startSlot + index), DefinitionId = definitionId, Level = level, Durability = 1 });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<Guid> FruitDefinitionAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Id\" FROM config.\"ItemDefinition\" WHERE \"Group\" = 13 AND \"Number\" = 15", connection);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }
}
