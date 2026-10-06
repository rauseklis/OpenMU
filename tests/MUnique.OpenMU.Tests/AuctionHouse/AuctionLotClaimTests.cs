// <copyright file="AuctionLotClaimTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using Npgsql;

/// <summary>
/// Verifies collecting an auctioned item against PostgreSQL: the item returns to a free main-inventory slot of the
/// claiming character, a character of another account is refused, and a replay does not place the item twice.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionLotClaimTests
{
    private string _connectionString = string.Empty;
    private Guid _accountId;
    private Guid _characterId;
    private Guid _inventoryId;
    private Guid _itemId;
    private Guid _collectionId;

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
        await using (var connection = new NpgsqlConnection(this._connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT c.\"Id\", c.\"AccountId\", c.\"InventoryId\" FROM data.\"Character\" c WHERE c.\"AccountId\" IS NOT NULL AND c.\"InventoryId\" IS NOT NULL LIMIT 1",
                connection);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                Assert.Ignore("The database has no character with an account; use the populated copy.");
            }

            this._characterId = reader.GetGuid(0);
            this._accountId = reader.GetGuid(1);
            this._inventoryId = reader.GetGuid(2);
        }

        this._itemId = Guid.NewGuid();
        this._collectionId = Guid.NewGuid();
        await using var context = new EntityDataContext();
        var definitionId = await context.Set<Item>().Where(i => i.DefinitionId != null).Select(i => i.DefinitionId).FirstOrDefaultAsync();
        if (definitionId is null)
        {
            Assert.Ignore("The database has no item definition; use the populated copy.");
        }

        var now = DateTimeOffset.UtcNow;
        context.Set<Item>().Add(new Item
        {
            Id = this._itemId,
            ItemStorageId = null,
            ItemSlot = 0,
            DefinitionId = definitionId,
            Durability = 10,
        });
        context.Set<AuctionCollection>().Add(new AuctionCollection
        {
            Id = this._collectionId,
            AccountId = this._accountId,
            Kind = 0,
            Currency = 0,
            ItemId = this._itemId,
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        });
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => c.Id == this._collectionId).ExecuteDeleteAsync();
        await context.Set<Item>().Where(i => i.Id == this._itemId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies the item is placed in a main-inventory slot of the claiming character, and the collection is claimed.
    /// </summary>
    [Test]
    public async Task ClaimPlacesItemInFreeInventorySlot()
    {
        var result = await this.Claim(Guid.NewGuid(), this._accountId, this._characterId);

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
        await using var context = new EntityDataContext();
        var item = await context.Set<Item>().AsNoTracking().SingleAsync(i => i.Id == this._itemId);
        var status = await context.Set<AuctionCollection>().AsNoTracking().Where(c => c.Id == this._collectionId).Select(c => c.Status).SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(item.ItemStorageId, Is.EqualTo(this._inventoryId));
            Assert.That(item.ItemSlot, Is.GreaterThanOrEqualTo(12));
            Assert.That(status, Is.EqualTo(2));
        });
    }

    /// <summary>
    /// Verifies a replayed claim does not place the item a second time.
    /// </summary>
    [Test]
    public async Task ReplayedClaimDoesNotPlaceTwice()
    {
        var operation = Guid.NewGuid();
        await this.Claim(operation, this._accountId, this._characterId);

        var replay = await this.Claim(operation, this._accountId, this._characterId);

        Assert.That(replay.Outcome, Is.EqualTo(AuctionClaimOutcome.Replayed));
    }

    /// <summary>
    /// Verifies a character which the claiming account does not own is refused, and the item stays outside the inventories.
    /// </summary>
    [Test]
    public async Task CharacterOfAnotherAccountIsRefused()
    {
        var result = await this.Claim(Guid.NewGuid(), Guid.NewGuid(), this._characterId);

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.NotFound));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<Item>().AsNoTracking().Where(i => i.Id == this._itemId).Select(i => i.ItemStorageId).SingleAsync(), Is.Null);
    }

    /// <summary>
    /// Verifies a partial quantity is refused: an item is claimed whole or not at all.
    /// </summary>
    [Test]
    public async Task PartialQuantityIsRefused()
    {
        var result = await new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(
            OperationId: Guid.NewGuid(),
            AccountId: this._accountId,
            CharacterId: this._characterId,
            CollectionId: this._collectionId,
            ExpectedVersion: 0,
            Now: DateTimeOffset.UtcNow,
            RequestedScalar: 1));

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.InvalidClaimQuantity));
    }

    private Task<AuctionClaimResult> Claim(Guid operationId, Guid accountId, Guid characterId)
    {
        return new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(
            OperationId: operationId,
            AccountId: accountId,
            CharacterId: characterId,
            CollectionId: this._collectionId,
            ExpectedVersion: 0,
            Now: DateTimeOffset.UtcNow));
    }
}
