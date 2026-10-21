// <copyright file="AuctionClaimLotPlacementTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies against PostgreSQL that a collected item is placed where it fits: large items never overlap what is in the inventory,
/// and a full inventory refuses the claim, so the Auction House can tell the player.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionClaimLotPlacementTests
{
    private static readonly int First = InventoryConstants.EquippableSlotsCount;

    private Guid _characterId;
    private Guid _accountId;
    private Guid _inventoryId;
    private Guid _largeDefinition;
    private Guid _smallDefinition;
    private readonly List<Guid> _createdItems = new();
    private readonly List<Guid> _createdCollections = new();

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
        var owners = await AuctionTestCharacters.FundOwnedAsync(AuctionPostgresTestEnvironment.ConnectionString!, 0, 1);
        if (owners.Count == 0)
        {
            Assert.Ignore("The database has no characters to use; use the populated copy.");
        }

        this._characterId = owners[0].CharacterId;
        this._accountId = owners[0].AccountId;
        await using var context = new EntityDataContext();
        this._inventoryId = await context.Set<Character>().Where(c => c.Id == this._characterId).Select(c => c.InventoryId!.Value).SingleAsync();
        var large = await context.Database.SqlQuery<Guid>($"SELECT d.\"Id\" AS \"Value\" FROM config.\"ItemDefinition\" d WHERE d.\"Width\" = 2 AND d.\"Height\" = 4 LIMIT 1").ToListAsync();
        var small = await context.Database.SqlQuery<Guid>($"SELECT d.\"Id\" AS \"Value\" FROM config.\"ItemDefinition\" d WHERE d.\"Width\" = 1 AND d.\"Height\" = 1 LIMIT 1").ToListAsync();
        if (large.Count == 0 || small.Count == 0)
        {
            Assert.Ignore("The database has no 2 x 4 and 1 x 1 item definitions.");
        }

        this._largeDefinition = large[0];
        this._smallDefinition = small[0];

        // start from an empty main inventory, so the test does not depend on the character's items.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE data.\"Item\" SET \"ItemStorageId\" = NULL WHERE \"ItemStorageId\" = {this._inventoryId} AND \"ItemSlot\" >= {(int)InventoryConstants.EquippableSlotsCount} AND \"Id\" NOT IN (SELECT \"ItemId\" FROM data.\"AuctionListing\" WHERE \"ItemId\" IS NOT NULL)");
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._accountId).ExecuteDeleteAsync();
        foreach (var collection in this._createdCollections)
        {
            await context.Set<AuctionCollection>().Where(c => c.Id == collection).ExecuteDeleteAsync();
        }

        foreach (var item in this._createdItems)
        {
            await context.Set<Item>().Where(i => i.Id == item).ExecuteDeleteAsync();
        }

        this._createdItems.Clear();
        this._createdCollections.Clear();
    }

    /// <summary>
    /// A large item which is collected must not land on the cells of another large item.
    /// </summary>
    [Test]
    public async Task LargeCollectedItemDoesNotOverlapAnotherLargeItem()
    {
        // a 2 x 4 item in the top left corner.
        await this.AddInventoryItemAsync(this._largeDefinition, First);
        var lot = await this.AddLotAsync(this._largeDefinition);

        var result = await this.ClaimAsync(lot);

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
        await using var context = new EntityDataContext();
        var slot = await context.Set<Item>().Where(i => i.Id == lot.ItemId!.Value).Select(i => (int)i.ItemSlot).SingleAsync();
        Assert.That(slot, Is.EqualTo(First + 2), "the next free position which fits the whole item, right of the first one");
    }

    /// <summary>
    /// A small item which is collected skips the cells which a large item covers.
    /// </summary>
    [Test]
    public async Task SmallCollectedItemSkipsTheCellsOfALargeItem()
    {
        await this.AddInventoryItemAsync(this._largeDefinition, First);
        var lot = await this.AddLotAsync(this._smallDefinition);

        var result = await this.ClaimAsync(lot);

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
        await using var context = new EntityDataContext();
        var slot = await context.Set<Item>().Where(i => i.Id == lot.ItemId!.Value).Select(i => (int)i.ItemSlot).SingleAsync();
        Assert.That(slot, Is.EqualTo(First + 2));
    }

    /// <summary>
    /// A large item does not fit into an inventory which still has single free cells, but no room for it.
    /// </summary>
    [Test]
    public async Task LargeItemIsRefusedWhenNoPositionFits()
    {
        // every second row is blocked by a full row of small items: there is no free 2 x 4 block.
        for (var row = 0; row < InventoryConstants.InventoryRows; row += 3)
        {
            for (var column = 0; column < InventoryConstants.RowSize; column++)
            {
                await this.AddInventoryItemAsync(this._smallDefinition, First + (row * InventoryConstants.RowSize) + column);
            }
        }

        var lot = await this.AddLotAsync(this._largeDefinition);

        var result = await this.ClaimAsync(lot);

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.InventoryFull));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<Item>().Where(i => i.Id == lot.ItemId!.Value).Select(i => i.ItemStorageId).SingleAsync(), Is.Null, "the item stays in the collection");
    }

    private async Task AddInventoryItemAsync(Guid definition, int slot)
    {
        await using var context = new EntityDataContext();
        var item = new Item { Id = Guid.NewGuid(), ItemStorageId = this._inventoryId, ItemSlot = (byte)slot, DefinitionId = definition, Durability = 1 };
        context.Set<Item>().Add(item);
        await context.SaveChangesAsync();
        this._createdItems.Add(item.Id);
    }

    private async Task<AuctionCollection> AddLotAsync(Guid definition)
    {
        await using var context = new EntityDataContext();
        var item = new Item { Id = Guid.NewGuid(), ItemStorageId = null, ItemSlot = 0, DefinitionId = definition, Durability = 1 };
        var now = DateTimeOffset.UtcNow;
        var collection = new AuctionCollection
        {
            Id = Guid.NewGuid(),
            AccountId = this._accountId,
            Kind = 0,
            Currency = 0,
            ItemId = item.Id,
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };
        context.Set<Item>().Add(item);
        context.Set<AuctionCollection>().Add(collection);
        await context.SaveChangesAsync();
        this._createdItems.Add(item.Id);
        this._createdCollections.Add(collection.Id);
        return collection;
    }

    private Task<AuctionClaimResult> ClaimAsync(AuctionCollection collection)
    {
        return new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(
            OperationId: Guid.NewGuid(),
            AccountId: this._accountId,
            CharacterId: this._characterId,
            CollectionId: collection.Id,
            ExpectedVersion: 0,
            Now: DateTimeOffset.UtcNow));
    }
}
