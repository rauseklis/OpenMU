// <copyright file="AuctionZenCreditTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using Npgsql;

/// <summary>
/// Verifies that claiming a Zen collection credits the claimant's inventory, records the credit in the ledger, is refused
/// when it would exceed the inventory money limit, and never credits twice.
/// Runs against the populated disposable copy, because it needs a real character with an inventory.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionZenCreditTests
{
    private const long RefundAmount = 1000;
    private const int MoneyLimit = 2_000_000_000;
    private string _connectionString = string.Empty;
    private Guid _characterId;
    private Guid _storageId;
    private int _originalMoney;
    private Guid _accountId;
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
        var funded = await AuctionTestCharacters.FundAsync(this._connectionString, 0);
        if (funded is null)
        {
            Assert.Ignore("The database has no characters to credit; use the populated copy.");
        }

        this._characterId = funded.Value;
        await using (var connection = new NpgsqlConnection(this._connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT s.\"Id\", s.\"Money\" FROM data.\"Character\" c JOIN data.\"ItemStorage\" s ON s.\"Id\" = c.\"InventoryId\" WHERE c.\"Id\" = @id", connection);
            command.Parameters.AddWithValue("id", this._characterId);
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            this._storageId = reader.GetGuid(0);
            this._originalMoney = reader.GetInt32(1);
        }

        this._accountId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var context = new EntityDataContext();
        var refund = new AuctionCollection
        {
            Id = Guid.NewGuid(),
            AccountId = this._accountId,
            Kind = 3,
            Currency = 0,
            OriginalScalar = RefundAmount,
            RemainingScalar = RefundAmount,
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };
        context.Set<AuctionCollection>().Add(refund);
        await context.SaveChangesAsync();
        this._collectionId = refund.Id;
    }

    [TearDown]
    public async Task Cleanup()
    {
        await this.SetMoney(this._originalMoney);
        await using var context = new EntityDataContext();
        await context.Set<AuctionCurrencyMovement>().Where(m => m.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => c.AccountId == this._accountId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies claiming a refund credits the claimant's Zen and records the credit in the ledger.
    /// </summary>
    [Test]
    public async Task ClaimCreditsClaimantZenAndRecordsLedger()
    {
        var operation = Guid.NewGuid();

        var result = await this.Claim(0, operation);

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
        Assert.That(await this.GetMoney(), Is.EqualTo(this._originalMoney + RefundAmount));
        await using var context = new EntityDataContext();
        var movement = await context.Set<AuctionCurrencyMovement>().AsNoTracking().SingleAsync(m => m.OperationId == operation);
        Assert.Multiple(() =>
        {
            Assert.That(movement.Amount, Is.EqualTo(RefundAmount));
            Assert.That(movement.Kind, Is.EqualTo(6));
        });
    }

    /// <summary>
    /// Verifies a claim that would push the character past the inventory money limit is refused, and the collection stays pending.
    /// </summary>
    [Test]
    public async Task ClaimOverTheMoneyLimitIsRefused()
    {
        await this.SetMoney(MoneyLimit - 500);

        var result = await this.Claim(0, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.MoneyLimit));
        Assert.That(await this.GetMoney(), Is.EqualTo(MoneyLimit - 500));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionCollection>().AsNoTracking().Where(c => c.Id == this._collectionId).Select(c => c.Status).SingleAsync(), Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies a replayed claim does not credit Zen a second time.
    /// </summary>
    [Test]
    public async Task ReplayedClaimDoesNotCreditTwice()
    {
        var operation = Guid.NewGuid();
        await this.Claim(0, operation);

        var replay = await this.Claim(1, operation);

        Assert.That(replay.Outcome, Is.EqualTo(AuctionClaimOutcome.Replayed));
        Assert.That(await this.GetMoney(), Is.EqualTo(this._originalMoney + RefundAmount));
    }

    private Task<AuctionClaimResult> Claim(int expectedVersion, Guid operationId)
    {
        return new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(
            OperationId: operationId,
            AccountId: this._accountId,
            CharacterId: this._characterId,
            CollectionId: this._collectionId,
            ExpectedVersion: expectedVersion,
            Now: DateTimeOffset.UtcNow));
    }

    private async Task<int> GetMoney()
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Money\" FROM data.\"ItemStorage\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", this._storageId);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task SetMoney(int money)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE data.\"ItemStorage\" SET \"Money\" = @money WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("money", money);
        command.Parameters.AddWithValue("id", this._storageId);
        await command.ExecuteNonQueryAsync();
    }
}
