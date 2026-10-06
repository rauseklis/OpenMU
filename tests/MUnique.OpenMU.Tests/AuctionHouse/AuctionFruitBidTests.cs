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
    private string _connectionString = string.Empty;
    private Guid _sellerAccountId;
    private Guid _firstAccountId;
    private Guid _firstCharacterId;
    private Guid _firstInventoryId;
    private Guid _secondAccountId;
    private Guid _secondCharacterId;
    private Guid _secondInventoryId;
    private Guid _fruitDefinitionId;
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
        this._fruitDefinitionId = await FruitDefinitionAsync(this._connectionString);

        await using var context = new EntityDataContext();
        this._firstInventoryId = (await context.Set<Character>().AsNoTracking().Where(c => c.Id == this._firstCharacterId).Select(c => c.InventoryId).SingleAsync())!.Value;
        this._secondInventoryId = (await context.Set<Character>().AsNoTracking().Where(c => c.Id == this._secondCharacterId).Select(c => c.InventoryId).SingleAsync())!.Value;
        await context.Set<Item>().Where(i => (i.ItemStorageId == this._firstInventoryId || i.ItemStorageId == this._secondInventoryId) && i.DefinitionId == this._fruitDefinitionId).ExecuteDeleteAsync();

        await AddFruitsAsync(context, this._firstInventoryId, this._fruitDefinitionId, level: 3, count: 2, startSlot: 70);
        await AddFruitsAsync(context, this._firstInventoryId, this._fruitDefinitionId, level: 0, count: 1, startSlot: 72);
        await AddFruitsAsync(context, this._secondInventoryId, this._fruitDefinitionId, level: 3, count: 3, startSlot: 70);
        await AddFruitsAsync(context, this._secondInventoryId, this._fruitDefinitionId, level: 0, count: 2, startSlot: 73);

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
        await context.Set<Item>().Where(i => (i.ItemStorageId == this._firstInventoryId || i.ItemStorageId == this._secondInventoryId) && i.DefinitionId == this._fruitDefinitionId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies a first bid at the starting basket is placed, and its fruits leave the bidder's inventory.
    /// </summary>
    [Test]
    public async Task FirstBidAtTheStartingBasketIsPlaced()
    {
        var result = await PlaceAsync(this._firstAccountId, this._firstCharacterId, version: 0, new FruitBasket(2, 0, 0, 1, 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<Item>().AsNoTracking().CountAsync(i => i.ItemStorageId == this._firstInventoryId && i.DefinitionId == this._fruitDefinitionId), Is.EqualTo(0));
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
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<Item>().AsNoTracking().CountAsync(i => i.ItemStorageId == this._secondInventoryId && i.DefinitionId == this._fruitDefinitionId), Is.EqualTo(5));
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

