// <copyright file="AuctionListItemTests.cs" company="MUnique">
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
/// Verifies listing an inventory item through the service against PostgreSQL: the fee is charged in Zen, the item leaves the
/// inventory, a repeated operation charges nothing again, and every refusal leaves the item and the Zen where they were.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionListItemTests
{
    private const byte FeeRemovedKind = 5;
    private string _connectionString = string.Empty;
    private Guid _accountId;
    private Guid _characterId;
    private Guid _inventoryId;
    private Guid _itemId;
    private byte _slot;

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
        var owners = await AuctionTestCharacters.FundOwnedAsync(this._connectionString, 10_000_000, 1);
        if (owners.Count == 0)
        {
            Assert.Ignore("The database has no character to list from; use the populated copy.");
        }

        this._accountId = owners[0].AccountId;
        this._characterId = owners[0].CharacterId;
        await using var context = new EntityDataContext();
        var inventoryId = await context.Set<Character>().Where(c => c.Id == this._characterId).Select(c => c.InventoryId).SingleAsync();
        this._inventoryId = inventoryId!.Value;
        var definitionId = await context.Set<Item>()
            .Where(i => i.DefinitionId != null && i.RawDefinition.IsTradable && i.RawDefinition.IsPersonalStoreSellable && !i.RawDefinition.IsBoundToCharacter && !i.RawDefinition.IsQuestItem)
            .Select(i => i.DefinitionId)
            .FirstOrDefaultAsync();
        if (definitionId is null)
        {
            Assert.Ignore("The database has no sellable item definition; use the populated copy.");
        }

        this._slot = await this.FreeSlotAsync(context);
        this._itemId = Guid.NewGuid();
        context.Set<Item>().Add(new Item
        {
            Id = this._itemId,
            ItemStorageId = this._inventoryId,
            ItemSlot = this._slot,
            DefinitionId = definitionId,
            Durability = 10,
        });
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionCurrencyMovement>().Where(m => m.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.SellerAccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<Item>().Where(i => i.Id == this._itemId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies a successful listing charges the 1% fee in Zen, takes the item out of the inventory, and records the listing and the fee.
    /// </summary>
    [Test]
    public async Task ListingChargesFeeAndTakesTheItemOutOfTheInventory()
    {
        var result = await this.CreateService().ListItemAsync(this.Request(startingPrice: 1000));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        await using var context = new EntityDataContext();
        var item = await context.Set<Item>().AsNoTracking().SingleAsync(i => i.Id == this._itemId);
        var listing = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == result.ListingId);
        var movement = await context.Set<AuctionCurrencyMovement>().AsNoTracking().SingleAsync(m => m.OperationId == this._operationForLastRequest);
        Assert.Multiple(() =>
        {
            Assert.That(item.ItemStorageId, Is.Null);
            Assert.That(listing.ItemId, Is.EqualTo(this._itemId));
            Assert.That(movement.Kind, Is.EqualTo(FeeRemovedKind));
            Assert.That(movement.Amount, Is.EqualTo(10));
        });
        Assert.That(await this.GetMoneyAsync(), Is.EqualTo(10_000_000 - 10));
    }

    /// <summary>
    /// Verifies a repeated operation returns the stored listing and does not charge the fee again.
    /// </summary>
    [Test]
    public async Task RepeatedOperationReturnsTheStoredListingWithoutSecondFee()
    {
        var service = this.CreateService();
        var request = this.Request(startingPrice: 1000);
        var first = await service.ListItemAsync(request);

        var second = await service.ListItemAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(second.Result, Is.EqualTo(AuctionResultCode.Success));
            Assert.That(second.ListingId, Is.EqualTo(first.ListingId));
        });
        Assert.That(await this.GetMoneyAsync(), Is.EqualTo(10_000_000 - 10));
    }

    /// <summary>
    /// Verifies a character who cannot pay the fee is refused and the item stays in the inventory.
    /// </summary>
    [Test]
    public async Task InsufficientZenRefusesAndKeepsTheItem()
    {
        await this.SetMoneyAsync(5);

        var result = await this.CreateService().ListItemAsync(this.Request(startingPrice: 1000));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.InsufficientCurrency));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<Item>().AsNoTracking().Where(i => i.Id == this._itemId).Select(i => i.ItemStorageId).SingleAsync(), Is.EqualTo(this._inventoryId));
    }

    /// <summary>
    /// Verifies an item with no durability left fails the eligibility check and is not listed or charged.
    /// </summary>
    [Test]
    public async Task ItemWithoutDurabilityIsNotEligible()
    {
        await using (var context = new EntityDataContext())
        {
            await context.Set<Item>().Where(i => i.Id == this._itemId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Durability, 0));
        }

        var result = await this.CreateService().ListItemAsync(this.Request(startingPrice: 1000));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.ItemNotEligible));
        Assert.That(await this.GetMoneyAsync(), Is.EqualTo(10_000_000));
    }

    /// <summary>
    /// Verifies an empty slot is reported as an item which was not found.
    /// </summary>
    [Test]
    public async Task EmptySlotIsItemNotFound()
    {
        var request = this.Request(startingPrice: 1000) with { InventorySlot = (byte)(this._slot == 75 ? 74 : this._slot + 1) };
        await using (var context = new EntityDataContext())
        {
            if (await context.Set<Item>().AnyAsync(i => i.ItemStorageId == this._inventoryId && i.ItemSlot == request.InventorySlot))
            {
                Assert.Ignore("The next slot is occupied in this copy of the database.");
            }
        }

        var result = await this.CreateService().ListItemAsync(request);

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.ItemNotFound));
    }

    /// <summary>
    /// Verifies the per-character listing limit is checked before any fee is charged.
    /// </summary>
    [Test]
    public async Task CharacterLimitRefusesBeforeAnyFee()
    {
        var result = await this.CreateService(perCharacter: 0).ListItemAsync(this.Request(startingPrice: 1000));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.ListingLimitReached));
        Assert.That(await this.GetMoneyAsync(), Is.EqualTo(10_000_000));
    }

    /// <summary>
    /// Verifies an item which is already listed cannot be listed again, even when it is put back into the inventory.
    /// </summary>
    [Test]
    public async Task AlreadyListedItemIsNotEligibleAgain()
    {
        var service = this.CreateService();
        await service.ListItemAsync(this.Request(startingPrice: 1000));
        await using (var context = new EntityDataContext())
        {
            await context.Set<Item>().Where(i => i.Id == this._itemId).ExecuteUpdateAsync(s => s.SetProperty(i => i.ItemStorageId, this._inventoryId));
        }

        var result = await service.ListItemAsync(this.Request(startingPrice: 1000));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.ItemNotEligible));
        Assert.That(await this.GetMoneyAsync(), Is.EqualTo(10_000_000 - 10));
    }

    /// <summary>
    /// Verifies a request is refused before the database when the currency, price, duration or feature is not valid.
    /// </summary>
    [Test]
    public async Task InvalidRequestsAreRefusedWithoutChange()
    {
        var service = this.CreateService();

        var fruits = await service.ListItemAsync(this.Request(startingPrice: 1000) with { Currency = 8 });
        var zeroPrice = await service.ListItemAsync(this.Request(startingPrice: 0));
        var badDuration = await service.ListItemAsync(this.Request(startingPrice: 1000) with { DurationHours = 5 });
        var disabled = await this.CreateService(enabled: false).ListItemAsync(this.Request(startingPrice: 1000));

        Assert.Multiple(() =>
        {
            Assert.That(fruits.Result, Is.EqualTo(AuctionResultCode.UnsupportedCurrency));
            Assert.That(zeroPrice.Result, Is.EqualTo(AuctionResultCode.InvalidPrice));
            Assert.That(badDuration.Result, Is.EqualTo(AuctionResultCode.InvalidDuration));
            Assert.That(disabled.Result, Is.EqualTo(AuctionResultCode.Disabled));
        });
        Assert.That(await this.GetMoneyAsync(), Is.EqualTo(10_000_000));
    }

    private Guid _operationForLastRequest;

    private AuctionHouseService CreateService(bool enabled = true, int perCharacter = 5)
    {
        return new AuctionHouseService(new AuctionHouseRepository(), new AuctionHouseOptions
        {
            Enabled = enabled,
            MaximumActiveListingsPerCharacter = perCharacter,
        });
    }

    private AuctionListItemRequest Request(long startingPrice)
    {
        this._operationForLastRequest = Guid.NewGuid();
        return new AuctionListItemRequest(
            OperationId: this._operationForLastRequest,
            AccountId: this._accountId,
            CharacterId: this._characterId,
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            InventorySlot: this._slot,
            Currency: 0,
            Category: 0,
            StartingPrice: startingPrice,
            BuyoutPrice: null,
            DurationHours: 24);
    }

    private async Task<byte> FreeSlotAsync(EntityDataContext context)
    {
        var used = await context.Set<Item>().AsNoTracking().Where(i => i.ItemStorageId == this._inventoryId).Select(i => i.ItemSlot).ToListAsync();
        return (byte)Enumerable.Range(InventoryConstants.EquippableSlotsCount, 64).First(slot => !used.Contains((byte)slot));
    }

    private async Task SetMoneyAsync(int money)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE data.\"ItemStorage\" SET \"Money\" = @money WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("money", money);
        command.Parameters.AddWithValue("id", this._inventoryId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> GetMoneyAsync()
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Money\" FROM data.\"ItemStorage\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", this._inventoryId);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
