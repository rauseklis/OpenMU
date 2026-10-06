// <copyright file="AuctionFruitListingTests.cs" company="MUnique">
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
/// Verifies listing a fruit basket against PostgreSQL: the per-component fee is paid from clean fruits at each component's level,
/// the starting basket is stored on the listing, and a basket the seller cannot pay for changes nothing.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionFruitListingTests
{
    private string _connectionString = string.Empty;
    private Guid _accountId;
    private Guid _characterId;
    private Guid _inventoryId;
    private Guid _fruitDefinitionId;
    private Guid _listedItemId;
    private Guid _listingId;
    private readonly List<Guid> _fruits = new();

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
        var owners = await AuctionTestCharacters.FundOwnedAsync(this._connectionString, 0, 1);
        if (owners.Count == 0)
        {
            Assert.Ignore("The database has no linked character; use the populated copy.");
        }

        this._accountId = owners[0].AccountId;
        this._characterId = owners[0].CharacterId;
        await using var context = new EntityDataContext();
        this._inventoryId = (await context.Set<Character>().AsNoTracking().Where(c => c.Id == this._characterId).Select(c => c.InventoryId).SingleAsync())!.Value;
        this._fruitDefinitionId = await FruitDefinitionAsync(this._connectionString);
        var listedDefinition = await context.Set<Item>()
            .Where(i => i.DefinitionId != null && i.DefinitionId != this._fruitDefinitionId)
            .Select(i => i.DefinitionId!.Value)
            .FirstAsync();

        await context.Set<Item>().Where(i => i.ItemStorageId == this._inventoryId && i.DefinitionId == this._fruitDefinitionId).ExecuteDeleteAsync();
        this._listedItemId = Guid.NewGuid();
        context.Set<Item>().Add(new Item { Id = this._listedItemId, ItemStorageId = this._inventoryId, ItemSlot = 73, DefinitionId = listedDefinition, Durability = 10 });
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionCurrencyMovement>().Where(m => m.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.SellerAccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<Item>().Where(i => i.ItemStorageId == this._inventoryId && i.DefinitionId == this._fruitDefinitionId).ExecuteDeleteAsync();
        await context.Set<Item>().Where(i => i.Id == this._listedItemId).ExecuteDeleteAsync();
        foreach (var fruit in this._fruits)
        {
            await context.Set<Item>().Where(i => i.Id == fruit).ExecuteDeleteAsync();
        }

        this._fruits.Clear();
    }

    /// <summary>
    /// Verifies a fruit basket pays one fruit of each nonzero component's fee from the fruit at that component's level, stores the
    /// starting basket on the listing, and records one fee movement per component.
    /// </summary>
    [Test]
    public async Task BasketFeeIsPaidPerComponentFromTheMatchingFruit()
    {
        await this.AddFruitAsync(level: 3, slot: 70);
        await this.AddFruitAsync(level: 3, slot: 71);
        await this.AddFruitAsync(level: 0, slot: 72);

        var result = await this.ListAsync(new FruitBasket(Strength: 2, Agility: 0, Vitality: 0, Energy: 1, Command: 0));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        await using var context = new EntityDataContext();
        var strengthLeft = await context.Set<Item>().AsNoTracking().CountAsync(i => i.ItemStorageId == this._inventoryId && i.DefinitionId == this._fruitDefinitionId && i.Level == 3);
        var energyLeft = await context.Set<Item>().AsNoTracking().CountAsync(i => i.ItemStorageId == this._inventoryId && i.DefinitionId == this._fruitDefinitionId && i.Level == 0);
        var listing = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == this._listingId);
        var feeRows = await context.Set<AuctionCurrencyMovement>().AsNoTracking().Where(m => m.OperationId == this._lastOperation && m.Kind == 5).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(strengthLeft, Is.EqualTo(1));
            Assert.That(energyLeft, Is.EqualTo(0));
            Assert.That(listing.StartingStrength, Is.EqualTo(2));
            Assert.That(listing.StartingEnergy, Is.EqualTo(1));
            Assert.That(feeRows.Select(m => m.FruitComponent).OrderBy(c => c), Is.EquivalentTo(new byte?[] { 0, 3 }));
            Assert.That(feeRows.All(m => m.Currency == 8 && m.Amount == 1), Is.True);
        });
    }

    /// <summary>
    /// Verifies a basket the seller cannot pay for is refused, and no fruit is consumed.
    /// </summary>
    [Test]
    public async Task BasketWithoutTheFruitsIsRefused()
    {
        await this.AddFruitAsync(level: 3, slot: 70);
        await this.AddFruitAsync(level: 3, slot: 71);

        var result = await this.ListAsync(new FruitBasket(Strength: 2, Agility: 0, Vitality: 0, Energy: 1, Command: 0));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.InsufficientCurrency));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<Item>().AsNoTracking().CountAsync(i => i.ItemStorageId == this._inventoryId && i.DefinitionId == this._fruitDefinitionId), Is.EqualTo(2));
    }

    private Guid _lastOperation;

    private async Task AddFruitAsync(byte level, byte slot)
    {
        var id = Guid.NewGuid();
        this._fruits.Add(id);
        await using var context = new EntityDataContext();
        context.Set<Item>().Add(new Item { Id = id, ItemStorageId = this._inventoryId, ItemSlot = slot, DefinitionId = this._fruitDefinitionId, Level = level, Durability = 1 });
        await context.SaveChangesAsync();
    }

    private async Task<AuctionListItemResult> ListAsync(FruitBasket basket)
    {
        this._lastOperation = Guid.NewGuid();
        var service = new AuctionHouseService(new AuctionHouseRepository(), new AuctionHouseOptions { Enabled = true });
        var result = await service.ListItemAsync(new AuctionListItemRequest(
            OperationId: this._lastOperation,
            AccountId: this._accountId,
            CharacterId: this._characterId,
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            InventorySlot: 73,
            Currency: 8,
            Category: 0,
            StartingPrice: 0,
            BuyoutPrice: null,
            DurationHours: 24,
            StartingFruits: basket));
        this._listingId = result.ListingId;
        return result;
    }

    private static async Task<Guid> FruitDefinitionAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Id\" FROM config.\"ItemDefinition\" WHERE \"Group\" = 13 AND \"Number\" = 15", connection);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }
}
