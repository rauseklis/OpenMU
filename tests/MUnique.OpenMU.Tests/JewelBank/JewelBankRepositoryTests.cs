// <copyright file="JewelBankRepositoryTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.JewelBank;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.JewelBank;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using MUnique.OpenMU.Persistence.JewelBank;
using MUnique.OpenMU.Tests.AuctionHouse;

/// <summary>
/// Verifies the Jewel Bank repository against a disposable PostgreSQL database which already has the Jewel Bank tables.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class JewelBankRepositoryTests
{
    private const long Cap = 1_000;
    private const byte Bless = 2;
    private const byte Chaos = 1;
    private const byte FruitStrength = 14;

    private static readonly int First = InventoryConstants.EquippableSlotsCount;

    private Guid _accountId;
    private Guid _characterId;
    private Guid _inventoryId;
    private JewelBankRepository _repository = null!;

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

        this._accountId = owners[0].AccountId;
        this._characterId = owners[0].CharacterId;
        this._repository = new JewelBankRepository();
        await using var context = new EntityDataContext();
        this._inventoryId = await context.Set<Character>().Where(c => c.Id == this._characterId).Select(c => c.InventoryId!.Value).SingleAsync();
        await this.ClearAsync();
    }

    [TearDown]
    public async Task Cleanup() => await this.ClearAsync();

    /// <summary>
    /// Loose jewels and fruits are moved into the bank, counted per kind, and removed from the inventory.
    /// </summary>
    [Test]
    public async Task DepositMovesBankableItemsAndCountsThem()
    {
        await this.AddItemAsync(14, 13, 0, First);
        await this.AddItemAsync(14, 13, 0, First + 1);
        await this.AddItemAsync(12, 15, 0, First + 2);
        await this.AddItemAsync(13, 15, 3, First + 3);

        var result = await this._repository.DepositFromInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), null, Cap);

        Assert.That(result.Result, Is.EqualTo(JewelBankResultCode.Success));
        Assert.That(result.Moved, Is.EquivalentTo(new Dictionary<byte, long> { [Bless] = 2, [Chaos] = 1, [FruitStrength] = 1 }));
        var state = await this._repository.GetStateAsync(this._accountId);
        Assert.That(state.CountOf(Bless), Is.EqualTo(2));
        Assert.That(state.CountOf(Chaos), Is.EqualTo(1));
        Assert.That(state.CountOf(FruitStrength), Is.EqualTo(1));
        Assert.That(await this.InventoryItemCountAsync(), Is.EqualTo(0));
    }

    /// <summary>
    /// A packed jewel is credited with the units it represents.
    /// </summary>
    [Test]
    public async Task DepositCountsAPackAsItsUnits()
    {
        await this.AddPackAsync(14, 13, 2, First);

        var result = await this._repository.DepositFromInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), null, Cap);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That((await this._repository.GetStateAsync(this._accountId)).CountOf(Bless), Is.EqualTo(30));
    }

    /// <summary>
    /// The same operation identifier applies nothing twice, even if the inventory has bankable items again.
    /// </summary>
    [Test]
    public async Task RepeatedOperationIdentifierAppliesOnce()
    {
        var operation = Guid.NewGuid();
        await this.AddItemAsync(14, 13, 0, First);
        await this._repository.DepositFromInventoryAsync(this._accountId, this._characterId, operation, null, Cap);
        await this.AddItemAsync(14, 13, 0, First);

        var replay = await this._repository.DepositFromInventoryAsync(this._accountId, this._characterId, operation, null, Cap);

        Assert.That(replay.Result, Is.EqualTo(JewelBankResultCode.Success));
        Assert.That(replay.Moved, Is.Empty, "a replay reports the stored outcome and moves nothing");
        Assert.That((await this._repository.GetStateAsync(this._accountId)).CountOf(Bless), Is.EqualTo(1));
        Assert.That(await this.InventoryItemCountAsync(), Is.EqualTo(1), "the second item stays in the inventory");
    }

    /// <summary>
    /// Upgraded jewels and items with options or sockets are not plain jewels and stay in the inventory.
    /// </summary>
    [Test]
    public async Task DepositLeavesNonPlainItems()
    {
        await this.AddItemAsync(14, 13, 1, First);
        await this.AddItemAsync(13, 15, 5, First + 1);
        await this.AddItemAsync(14, 13, 0, First + 2, socketCount: 1);
        await this.AddItemAsync(14, 13, 0, First + 3, durability: 5);

        var result = await this._repository.DepositFromInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), null, Cap);

        Assert.That(result.Result, Is.EqualTo(JewelBankResultCode.NothingToDeposit));
        Assert.That(await this.InventoryItemCountAsync(), Is.EqualTo(4), "a possible stack stays too");
    }

    /// <summary>
    /// A deposit never takes the bank above its cap; the items which do not fit stay in the inventory.
    /// </summary>
    [Test]
    public async Task DepositStopsAtTheCap()
    {
        for (var i = 0; i < 5; i++)
        {
            await this.AddItemAsync(14, 13, 0, First + i);
        }

        var result = await this._repository.DepositFromInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), null, 3);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Moved[Bless], Is.EqualTo(3));
        Assert.That(await this.InventoryItemCountAsync(), Is.EqualTo(2));
        var full = await this._repository.DepositFromInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), null, 3);
        Assert.That(full.Result, Is.EqualTo(JewelBankResultCode.CapReached));
        Assert.That(await this.InventoryItemCountAsync(), Is.EqualTo(2));
    }

    /// <summary>
    /// A deposit restricted to one kind leaves the other kinds in the inventory.
    /// </summary>
    [Test]
    public async Task DepositCanBeLimitedToOneKind()
    {
        await this.AddItemAsync(14, 13, 0, First);
        await this.AddItemAsync(12, 15, 0, First + 1);

        var result = await this._repository.DepositFromInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), Chaos, Cap);

        Assert.That(result.Moved.Keys, Is.EquivalentTo(new[] { Chaos }));
        Assert.That(await this.InventoryItemCountAsync(), Is.EqualTo(1));
    }

    /// <summary>
    /// A withdrawal creates the largest packs first and the rest as loose jewels, and reduces the balance by exactly that.
    /// </summary>
    [Test]
    public async Task WithdrawCreatesPacksFirst()
    {
        await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 65, Cap);

        var result = await this._repository.WithdrawToInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), Bless, 65);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That((await this._repository.GetStateAsync(this._accountId)).CountOf(Bless), Is.EqualTo(0));
        var items = await this.InventoryAsync();
        Assert.That(items.Count(i => i.Group == 12 && i.Level == 2), Is.EqualTo(2), "two packs of 30");
        Assert.That(items.Count(i => i.Group == 14 && i.Number == 13 && i.Level == 0), Is.EqualTo(5), "five loose jewels");
    }

    /// <summary>
    /// Withdrawing a fruit creates a fruit of the right type per unit.
    /// </summary>
    [Test]
    public async Task WithdrawCreatesFruitsOfTheKindsLevel()
    {
        await this._repository.CreditPickupAsync(this._accountId, this._characterId, FruitStrength, 3, Cap);

        var result = await this._repository.WithdrawToInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), FruitStrength, 2);

        Assert.That(result.IsSuccess, Is.True);
        var items = await this.InventoryAsync();
        Assert.That(items.Select(i => (i.Group, i.Number, i.Level)), Is.EqualTo(new[] { (13, 15, 3), (13, 15, 3) }));
        Assert.That((await this._repository.GetStateAsync(this._accountId)).CountOf(FruitStrength), Is.EqualTo(1));
    }

    /// <summary>
    /// A withdrawal of more than the bank holds changes nothing.
    /// </summary>
    [Test]
    public async Task WithdrawBeyondTheBalanceChangesNothing()
    {
        await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 5, Cap);

        var result = await this._repository.WithdrawToInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), Bless, 6);

        Assert.That(result.Result, Is.EqualTo(JewelBankResultCode.InsufficientBalance));
        Assert.That((await this._repository.GetStateAsync(this._accountId)).CountOf(Bless), Is.EqualTo(5));
        Assert.That(await this.InventoryItemCountAsync(), Is.EqualTo(0));
    }

    /// <summary>
    /// A withdrawal which does not fit into the inventory changes nothing: the balance is kept and no item appears.
    /// </summary>
    [Test]
    public async Task WithdrawIntoAFullInventoryChangesNothing()
    {
        await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 5, Cap);
        for (var slot = First; slot < First + (InventoryConstants.InventoryRows * InventoryConstants.RowSize); slot++)
        {
            await this.AddItemAsync(14, 13, 1, slot);
        }

        var before = await this.InventoryItemCountAsync();
        var result = await this._repository.WithdrawToInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), Bless, 5);

        Assert.That(result.Result, Is.EqualTo(JewelBankResultCode.InventoryFull));
        Assert.That((await this._repository.GetStateAsync(this._accountId)).CountOf(Bless), Is.EqualTo(5));
        Assert.That(await this.InventoryItemCountAsync(), Is.EqualTo(before));
        var retry = await this._repository.WithdrawToInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), Bless, 5);
        Assert.That(retry.Result, Is.EqualTo(JewelBankResultCode.InventoryFull), "a refusal is not remembered as done");
    }

    /// <summary>
    /// A repeated withdrawal identifier does not hand out the items twice.
    /// </summary>
    [Test]
    public async Task RepeatedWithdrawAppliesOnce()
    {
        await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 10, Cap);
        var operation = Guid.NewGuid();

        await this._repository.WithdrawToInventoryAsync(this._accountId, this._characterId, operation, Bless, 4);
        var replay = await this._repository.WithdrawToInventoryAsync(this._accountId, this._characterId, operation, Bless, 4);

        Assert.That(replay.Result, Is.EqualTo(JewelBankResultCode.Success));
        Assert.That((await this._repository.GetStateAsync(this._accountId)).CountOf(Bless), Is.EqualTo(6));
        Assert.That(await this.InventoryItemCountAsync(), Is.EqualTo(4));
    }

    /// <summary>
    /// Invalid requests are refused before anything is touched.
    /// </summary>
    [TestCase((byte)0, 1L)]
    [TestCase((byte)200, 1L)]
    [TestCase(Bless, 0L)]
    [TestCase(Bless, -5L)]
    [TestCase(Bless, 100_001L)]
    public async Task InvalidWithdrawIsRefused(byte kind, long units)
    {
        await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 5, Cap);

        var result = await this._repository.WithdrawToInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), kind, units);

        Assert.That(result.Result, Is.EqualTo(JewelBankResultCode.InvalidRequest));
        Assert.That((await this._repository.GetStateAsync(this._accountId)).CountOf(Bless), Is.EqualTo(5));
    }

    /// <summary>
    /// A picked-up unit is credited, and refused (not partly credited) when it would exceed the cap.
    /// </summary>
    [Test]
    public async Task PickupCreditRespectsTheCap()
    {
        Assert.That(await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 2, 3), Is.EqualTo(2));
        Assert.That(await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 1, 3), Is.EqualTo(3));

        Assert.That(await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 1, 3), Is.Null);
        Assert.That((await this._repository.GetStateAsync(this._accountId)).CountOf(Bless), Is.EqualTo(3));
        Assert.That(await this._repository.CreditPickupAsync(this._accountId, this._characterId, 0, 1, 3), Is.Null, "unknown kind");
        Assert.That(await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 0, 3), Is.Null, "zero units");
    }

    /// <summary>
    /// Many concurrent withdrawals can never take more than the balance holds.
    /// </summary>
    [Test]
    public async Task ConcurrentWithdrawalsNeverOverdraw()
    {
        await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 5, Cap);

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            new JewelBankRepository().WithdrawToInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), Bless, 1)));

        Assert.That(results.Count(r => r.IsSuccess), Is.LessThanOrEqualTo(5));
        var state = await this._repository.GetStateAsync(this._accountId);
        Assert.That(state.CountOf(Bless), Is.EqualTo(5 - results.Count(r => r.IsSuccess)));
        Assert.That(state.CountOf(Bless), Is.GreaterThanOrEqualTo(0));
        Assert.That(await this._repository.ReconcileAsync(), Is.Empty);
    }

    /// <summary>
    /// The auto-deposit choice is stored per account and defaults to off.
    /// </summary>
    [Test]
    public async Task AutoDepositChoiceIsStored()
    {
        Assert.That((await this._repository.GetStateAsync(this._accountId)).AutoDeposit, Is.False);

        await this._repository.SetAutoDepositAsync(this._accountId, true);
        Assert.That((await this._repository.GetStateAsync(this._accountId)).AutoDeposit, Is.True);

        await this._repository.SetAutoDepositAsync(this._accountId, false);
        Assert.That((await this._repository.GetStateAsync(this._accountId)).AutoDeposit, Is.False);
    }

    /// <summary>
    /// Balances always equal the sum of their ledger movements.
    /// </summary>
    [Test]
    public async Task LedgerAgreesWithTheBalances()
    {
        await this.AddItemAsync(14, 13, 0, First);
        await this.AddItemAsync(14, 13, 0, First + 1);
        await this._repository.DepositFromInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), null, Cap);
        await this._repository.CreditPickupAsync(this._accountId, this._characterId, Bless, 7, Cap);
        await this._repository.WithdrawToInventoryAsync(this._accountId, this._characterId, Guid.NewGuid(), Bless, 4);

        Assert.That(await this._repository.ReconcileAsync(), Is.Empty);
        Assert.That((await this._repository.GetStateAsync(this._accountId)).CountOf(Bless), Is.EqualTo(5));
    }

    private async Task ClearAsync()
    {
        await using var context = new EntityDataContext();
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM data.\"JewelBankMovement\" WHERE \"AccountId\" = {this._accountId}");
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM data.\"JewelBankOperation\" WHERE \"AccountId\" = {this._accountId}");
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM data.\"JewelBankBalance\" WHERE \"AccountId\" = {this._accountId}");
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM data.\"JewelBankSettings\" WHERE \"AccountId\" = {this._accountId}");
        if (this._inventoryId != Guid.Empty)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM data.\"Item\" WHERE \"ItemStorageId\" = {this._inventoryId} AND \"ItemSlot\" >= {(int)InventoryConstants.EquippableSlotsCount} AND \"Id\" NOT IN (SELECT \"ItemId\" FROM data.\"AuctionListing\" WHERE \"ItemId\" IS NOT NULL)");
        }
    }

    private async Task<Guid> DefinitionAsync(int group, int number)
    {
        await using var context = new EntityDataContext();
        var ids = await context.Database.SqlQuery<Guid>($"SELECT d.\"Id\" AS \"Value\" FROM config.\"ItemDefinition\" d WHERE d.\"Group\" = {group} AND d.\"Number\" = {number}").ToListAsync();
        return ids.Single();
    }

    private async Task AddItemAsync(int group, int number, int level, int slot, int socketCount = 0, double durability = 1)
    {
        var definition = await this.DefinitionAsync(group, number);
        await using var context = new EntityDataContext();
        context.Set<Item>().Add(new Item
        {
            Id = Guid.NewGuid(),
            ItemStorageId = this._inventoryId,
            ItemSlot = (byte)slot,
            DefinitionId = definition,
            Level = (byte)level,
            Durability = durability,
            SocketCount = socketCount,
        });
        await context.SaveChangesAsync();
    }

    private async Task AddPackAsync(int group, int number, int level, int slot)
    {
        await using var context = new EntityDataContext();
        var single = await this.DefinitionAsync(group, number);
        var pack = (await context.Database.SqlQuery<Guid>($"SELECT jm.\"MixedJewelId\" AS \"Value\" FROM config.\"JewelMix\" jm WHERE jm.\"SingleJewelId\" = {single}").ToListAsync()).Single();
        context.Set<Item>().Add(new Item
        {
            Id = Guid.NewGuid(),
            ItemStorageId = this._inventoryId,
            ItemSlot = (byte)slot,
            DefinitionId = pack,
            Level = (byte)level,
            Durability = 1,
        });
        await context.SaveChangesAsync();
    }

    private async Task<int> InventoryItemCountAsync() => (await this.InventoryAsync()).Count;

    private async Task<IReadOnlyList<(int Group, int Number, int Level)>> InventoryAsync()
    {
        await using var context = new EntityDataContext();
        var rows = await context.Database.SqlQuery<InventoryRow>(
                $"SELECT d.\"Group\"::int AS \"Group\", d.\"Number\"::int AS \"Number\", i.\"Level\"::int AS \"Level\" FROM data.\"Item\" i JOIN config.\"ItemDefinition\" d ON d.\"Id\" = i.\"DefinitionId\" WHERE i.\"ItemStorageId\" = {this._inventoryId} AND i.\"ItemSlot\" >= {(int)InventoryConstants.EquippableSlotsCount} ORDER BY i.\"ItemSlot\"")
            .ToListAsync();
        return rows.Select(r => (r.Group, r.Number, r.Level)).ToList();
    }

    private sealed class InventoryRow
    {
        public int Group { get; set; }

        public int Number { get; set; }

        public int Level { get; set; }
    }
}
