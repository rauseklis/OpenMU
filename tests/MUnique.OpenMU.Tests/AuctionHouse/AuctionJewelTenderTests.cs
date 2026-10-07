// <copyright file="AuctionJewelTenderTests.cs" company="MUnique">
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
/// Verifies jewel tender against PostgreSQL: a jewel listing pays its fee from the seller's loose jewels and packs, change is
/// issued as a collection, and a claimed jewel balance is materialized as packs and loose jewels.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionJewelTenderTests
{
    private string _connectionString = string.Empty;
    private Guid _accountId;
    private Guid _characterId;
    private Guid _inventoryId;
    private Guid _looseDefinitionId;
    private Guid _packDefinitionId;
    private Guid _otherDefinitionId;
    private readonly List<Guid> _items = new();
    private readonly List<Guid> _collections = new();

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
        await using (var connection = new NpgsqlConnection(this._connectionString))
        {
            await connection.OpenAsync();
            await using (var command = new NpgsqlCommand("SELECT \"InventoryId\" FROM data.\"Character\" WHERE \"Id\" = @id", connection))
            {
                command.Parameters.AddWithValue("id", this._characterId);
                this._inventoryId = (Guid)(await command.ExecuteScalarAsync())!;
            }

            await using var defs = new NpgsqlCommand(
                "SELECT d.\"Id\", jm.\"MixedJewelId\" FROM config.\"ItemDefinition\" d JOIN config.\"JewelMix\" jm ON jm.\"SingleJewelId\" = d.\"Id\" WHERE d.\"Group\" = 12 AND d.\"Number\" = 15",
                connection);
            await using var reader = await defs.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                Assert.Ignore("The database has no Chaos jewel mix; use the populated copy.");
            }

            this._looseDefinitionId = reader.GetGuid(0);
            this._packDefinitionId = reader.GetGuid(1);
        }

        await using var context = new EntityDataContext();
        this._otherDefinitionId = await context.Set<Item>()
            .Where(i => i.DefinitionId != null && i.DefinitionId != this._looseDefinitionId && i.DefinitionId != this._packDefinitionId)
            .Select(i => i.DefinitionId!.Value)
            .FirstAsync();

        // The character starts with no jewels, so the planner's choice is deterministic.
        await context.Set<Item>().Where(i => i.ItemStorageId == this._inventoryId && (i.DefinitionId == this._looseDefinitionId || i.DefinitionId == this._packDefinitionId)).ExecuteDeleteAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionCurrencyMovement>().Where(m => m.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.SellerAccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => c.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => this._listingIds.Contains(b.ListingId)).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => this._listingIds.Contains(l.Id)).ExecuteDeleteAsync();
        this._listingIds.Clear();
        await context.Set<Item>().Where(i => this._items.Contains(i.Id)).ExecuteDeleteAsync();
        this._items.Clear();
    }

    /// <summary>
    /// Verifies a jewel listing pays its one-unit fee from a loose jewel, and the consumed jewel is recorded in the ledger.
    /// </summary>
    [Test]
    public async Task ListingFeeIsPaidFromALooseJewel()
    {
        var jewel = await this.AddItemAsync(this._looseDefinitionId, level: 0, slot: 70);
        var listed = await this.AddItemAsync(this._otherDefinitionId, level: 0, slot: 71);

        var result = await this.ListAsync(listed, startingPrice: 2);

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<Item>().AsNoTracking().AnyAsync(i => i.Id == jewel), Is.False);
        var tender = await context.Set<AuctionCurrencyMovement>().AsNoTracking().SingleAsync(m => m.OperationId == this._lastOperation && m.Kind == 0);
        Assert.Multiple(() =>
        {
            Assert.That(tender.Amount, Is.EqualTo(1));
            Assert.That(tender.Currency, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies paying with a pack returns the difference as a change collection.
    /// </summary>
    [Test]
    public async Task OverpaymentIsReturnedAsChange()
    {
        var pack = await this.AddItemAsync(this._packDefinitionId, level: 1, slot: 70);
        var listed = await this.AddItemAsync(this._otherDefinitionId, level: 0, slot: 71);

        var result = await this.ListAsync(listed, startingPrice: 2);

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<Item>().AsNoTracking().AnyAsync(i => i.Id == pack), Is.False);
        var change = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._accountId && c.Kind == 4);
        Assert.Multiple(() =>
        {
            Assert.That(change.OriginalScalar, Is.EqualTo(19));
            Assert.That(change.Currency, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies claiming a jewel balance creates the largest packs first and loose jewels for the rest.
    /// </summary>
    [Test]
    public async Task ClaimingJewelsMaterializesPacksThenLooseJewels()
    {
        var collection = Guid.NewGuid();
        await using (var context = new EntityDataContext())
        {
            var now = DateTimeOffset.UtcNow;
            context.Set<AuctionCollection>().Add(new AuctionCollection
            {
                Id = collection,
                AccountId = this._accountId,
                Kind = 2,
                Currency = 1,
                OriginalScalar = 25,
                RemainingScalar = 25,
                Status = 0,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 0,
            });
            await context.SaveChangesAsync();
        }

        this._collections.Add(collection);
        var result = await new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(
            OperationId: Guid.NewGuid(),
            AccountId: this._accountId,
            CharacterId: this._characterId,
            CollectionId: collection,
            ExpectedVersion: 0,
            Now: DateTimeOffset.UtcNow));

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
        await using var check = new EntityDataContext();
        var packs = await check.Set<Item>().AsNoTracking().CountAsync(i => i.ItemStorageId == this._inventoryId && i.DefinitionId == this._packDefinitionId && i.Level == 1);
        var loose = await check.Set<Item>().AsNoTracking().CountAsync(i => i.ItemStorageId == this._inventoryId && i.DefinitionId == this._looseDefinitionId);
        Assert.Multiple(() =>
        {
            Assert.That(packs, Is.EqualTo(1));
            Assert.That(loose, Is.EqualTo(5));
        });
    }

    /// <summary>
    /// Verifies a jewel listing is refused when the seller has no jewels to pay the fee, and nothing is listed.
    /// </summary>
    [Test]
    public async Task ListingWithoutJewelsIsRefused()
    {
        var listed = await this.AddItemAsync(this._otherDefinitionId, level: 0, slot: 71);

        var result = await this.ListAsync(listed, startingPrice: 2);

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.InsufficientCurrency));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<Item>().AsNoTracking().Where(i => i.Id == listed).Select(i => i.ItemStorageId).SingleAsync(), Is.EqualTo(this._inventoryId));
    }

    /// <summary>
    /// Verifies the persistence jewel identities agree with the GameLogic currency catalog for every jewel mode.
    /// </summary>
    [Test]
    public void JewelIdentitiesMatchTheCurrencyCatalog()
    {
        for (byte mode = 1; mode <= 7; mode++)
        {
            var catalog = CurrencyCatalog.GetLooseIdentity((AuctionCurrencyMode)mode)!.Value;
            var persistence = AuctionJewelIdentities.GetLooseJewel(mode)!.Value;
            Assert.That(persistence.Group, Is.EqualTo((byte)catalog.Group), $"group of mode {mode}");
            Assert.That(persistence.Number, Is.EqualTo((short)catalog.Number), $"number of mode {mode}");
        }
    }

    /// <summary>
    /// Verifies a jewel bid is escrowed from the bidder's own jewels: the consumed loose jewels leave the bidder's inventory,
    /// and the bid is placed at the listing.
    /// </summary>
    [Test]
    public async Task JewelBidIsEscrowedFromTheBiddersJewels()
    {
        var owners = await AuctionTestCharacters.FundOwnedAsync(this._connectionString, 0, 2);
        if (owners.Count < 2)
        {
            Assert.Ignore("The database has too few linked characters; use the populated copy.");
        }

        var bidder = owners[1];
        var bidderInventory = await this.InventoryOfAsync(bidder.CharacterId);
        var now = DateTimeOffset.UtcNow;
        var jewels = new List<Guid>();
        await using (var context = new EntityDataContext())
        {
            // Clear by slot, not just by definition: against a populated copy of the real database, these
            // fixed slots may already hold some other, unrelated real item.
            await context.Set<Item>().Where(i => i.ItemStorageId == bidderInventory && i.ItemSlot >= 70 && i.ItemSlot < 72).ExecuteDeleteAsync();
            for (var slot = 70; slot < 72; slot++)
            {
                var id = Guid.NewGuid();
                jewels.Add(id);
                context.Set<Item>().Add(new Item { Id = id, ItemStorageId = bidderInventory, ItemSlot = (byte)slot, DefinitionId = this._looseDefinitionId, Level = 0, Durability = 1 });
            }

            await context.SaveChangesAsync();
        }

        var listing = await new AuctionHouseRepository().CreateListingAsync(new EntityDataContext(), new AuctionListingDraft(
            OperationId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            ItemId: Guid.NewGuid(),
            SellerCharacterId: Guid.NewGuid(),
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            Currency: 1,
            Category: 0,
            StartingPrice: 2,
            CreatedAt: now,
            EndsAt: now.AddHours(24)));
        this._listingIds.Add(listing.ListingId);

        var bid = await new AuctionHouseRepository().PlaceScalarBidAsync(new EntityDataContext(), new ScalarBidDraft(
            OperationId: Guid.NewGuid(),
            BidderAccountId: bidder.AccountId,
            BidderCharacterId: bidder.CharacterId,
            ListingId: listing.ListingId,
            ExpectedVersion: 0,
            BidAmount: 2,
            MinimumBid: 2,
            BidderIp: "127.0.0.1",
            Now: now,
            NewEndsAt: now.AddHours(24)));

        Assert.That(bid.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));
        await using var check = new EntityDataContext();
        Assert.That(await check.Set<Item>().AsNoTracking().CountAsync(i => jewels.Contains(i.Id)), Is.EqualTo(0));
    }

    private readonly List<Guid> _listingIds = new();

    private async Task<Guid> InventoryOfAsync(Guid characterId)
    {
        await using var context = new EntityDataContext();
        return (await context.Set<Character>().AsNoTracking().Where(c => c.Id == characterId).Select(c => c.InventoryId).SingleAsync())!.Value;
    }

    private Guid _lastOperation;

    private async Task<Guid> AddItemAsync(Guid definitionId, byte level, byte slot)
    {
        var id = Guid.NewGuid();
        await using var context = new EntityDataContext();
        // Against a populated copy of the real database, this inventory's slot may already hold a real item.
        // Clear it first so the fixed test slot is deterministic, same as the rest of this test's own arrange
        // step (it already overwrites this character's Zen unconditionally).
        await context.Set<Item>().Where(i => i.ItemStorageId == this._inventoryId && i.ItemSlot == slot).ExecuteDeleteAsync();
        context.Set<Item>().Add(new Item
        {
            Id = id,
            ItemStorageId = this._inventoryId,
            ItemSlot = slot,
            DefinitionId = definitionId,
            Level = level,
            Durability = 1,
        });
        await context.SaveChangesAsync();
        this._items.Add(id);
        return id;
    }

    private async Task<AuctionListItemResult> ListAsync(Guid itemId, long startingPrice)
    {
        var slot = await this.SlotOfAsync(itemId);
        this._lastOperation = Guid.NewGuid();
        var service = new AuctionHouseService(new AuctionHouseRepository(), new AuctionHouseOptions { Enabled = true });
        return await service.ListItemAsync(new AuctionListItemRequest(
            OperationId: this._lastOperation,
            AccountId: this._accountId,
            CharacterId: this._characterId,
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            InventorySlot: slot,
            Currency: 1,
            Category: 0,
            StartingPrice: startingPrice,
            BuyoutPrice: null,
            DurationHours: 24));
    }

    private async Task<byte> SlotOfAsync(Guid itemId)
    {
        await using var context = new EntityDataContext();
        return await context.Set<Item>().AsNoTracking().Where(i => i.Id == itemId).Select(i => i.ItemSlot).SingleAsync();
    }
}
