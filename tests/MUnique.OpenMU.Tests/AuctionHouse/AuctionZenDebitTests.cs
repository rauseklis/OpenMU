// <copyright file="AuctionZenDebitTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using Npgsql;

/// <summary>
/// Verifies that bids and buyouts debit the bidder's Zen from the character inventory, and record it in the ledger.
/// Runs against the populated disposable copy of the server database, because it needs real characters with inventories.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionZenDebitTests
{
    private const long StartingPrice = 1000;
    private const long BuyoutPrice = 5000;
    private const long SuccessFee = 100;
    private string _connectionString = string.Empty;
    private Guid _storageA;
    private Guid _storageB;
    private int _originalMoneyA;
    private int _originalMoneyB;
    private Guid _sellerAccountId;
    private Guid _accountA;
    private Guid _accountB;
    private Guid _listingId;
    private int _version;

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
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand(
            "SELECT c.\"InventoryId\", s.\"Money\", c2.\"InventoryId\", s2.\"Money\" FROM data.\"Character\" c " +
            "JOIN data.\"ItemStorage\" s ON s.\"Id\" = c.\"InventoryId\" " +
            "JOIN data.\"Character\" c2 ON c2.\"Id\" <> c.\"Id\" JOIN data.\"ItemStorage\" s2 ON s2.\"Id\" = c2.\"InventoryId\" " +
            "WHERE c.\"InventoryId\" IS NOT NULL AND c2.\"InventoryId\" IS NOT NULL LIMIT 1", connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            this._storageA = reader.GetGuid(0);
            this._originalMoneyA = reader.GetInt32(1);
            this._storageB = reader.GetGuid(2);
            this._originalMoneyB = reader.GetInt32(3);
        }

        await this.SetMoney(this._storageA, 5000);
        await this.SetMoney(this._storageB, 5000);

        this._sellerAccountId = Guid.NewGuid();
        this._accountA = Guid.NewGuid();
        this._accountB = Guid.NewGuid();
        this._version = 0;
        var now = DateTimeOffset.UtcNow;
        var created = await new AuctionHouseRepository().CreateListingAsync(new EntityDataContext(), new AuctionListingDraft(
            OperationId: Guid.NewGuid(),
            AccountId: this._sellerAccountId,
            ItemId: Guid.NewGuid(),
            SellerCharacterId: Guid.NewGuid(),
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            Currency: 0,
            Category: 0,
            StartingPrice: StartingPrice,
            CreatedAt: now,
            EndsAt: now.AddHours(24),
            BuyoutPrice: BuyoutPrice));
        this._listingId = created.ListingId;
    }

    [TearDown]
    public async Task Cleanup()
    {
        await this.SetMoney(this._storageA, this._originalMoneyA);
        await this.SetMoney(this._storageB, this._originalMoneyB);
        await using var context = new EntityDataContext();
        await context.Set<AuctionCollection>().Where(c => c.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionCurrencyMovement>().Where(m => m.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => b.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.Id == this._listingId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies a bid debits the bidder's Zen by the bid amount and records the escrow in the ledger.
    /// </summary>
    [Test]
    public async Task BidDebitsBidderZenAndRecordsEscrow()
    {
        var operation = Guid.NewGuid();

        var result = await this.Bid(this._accountA, this._storageA, 1000, operation);

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));
        Assert.That(await this.GetMoney(this._storageA), Is.EqualTo(4000));
        await using var context = new EntityDataContext();
        var movement = await context.Set<AuctionCurrencyMovement>().AsNoTracking().SingleAsync(m => m.OperationId == operation);
        Assert.Multiple(() =>
        {
            Assert.That(movement.Amount, Is.EqualTo(1000));
            Assert.That(movement.Kind, Is.EqualTo(2));
        });
    }

    /// <summary>
    /// Verifies a bid the bidder cannot pay for is refused, with no Zen moved and no bid placed.
    /// </summary>
    [Test]
    public async Task BidWithoutEnoughZenIsRefused()
    {
        await this.SetMoney(this._storageA, 500);

        var result = await this.Bid(this._accountA, this._storageA, 1000, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.InsufficientCurrency));
        Assert.That(await this.GetMoney(this._storageA), Is.EqualTo(500));
    }

    /// <summary>
    /// Verifies a displaced bid's Zen is not credited back early: it waits in the refund collection until it is claimed.
    /// </summary>
    [Test]
    public async Task DisplacedBidderIsNotCreditedBeforeClaiming()
    {
        await this.Bid(this._accountA, this._storageA, 1000, Guid.NewGuid());
        this._version++;

        await this.Bid(this._accountB, this._storageB, 1100, Guid.NewGuid());

        Assert.That(await this.GetMoney(this._storageA), Is.EqualTo(4000));
        Assert.That(await this.GetMoney(this._storageB), Is.EqualTo(3900));
    }

    /// <summary>
    /// Verifies a buyout by another buyer debits the full buyout price.
    /// </summary>
    [Test]
    public async Task OtherBuyerPaysTheFullBuyoutPrice()
    {
        var result = await this.Buy(this._accountB, this._storageB, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        Assert.That(await this.GetMoney(this._storageB), Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies a high bidder who buys out pays only the difference, because its escrow was already debited.
    /// </summary>
    [Test]
    public async Task HighBidderPaysOnlyTheDifference()
    {
        await this.Bid(this._accountA, this._storageA, 1000, Guid.NewGuid());
        this._version++;

        var result = await this.Buy(this._accountA, this._storageA, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        Assert.That(await this.GetMoney(this._storageA), Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies a jewel listing is refused until jewel tender is implemented, so no escrow is ever taken without its debit.
    /// </summary>
    [Test]
    public async Task JewelListingBidIsRefusedUntilJewelTenderExists()
    {
        var now = DateTimeOffset.UtcNow;
        var jewel = await new AuctionHouseRepository().CreateListingAsync(new EntityDataContext(), new AuctionListingDraft(
            OperationId: Guid.NewGuid(),
            AccountId: this._sellerAccountId,
            ItemId: Guid.NewGuid(),
            SellerCharacterId: Guid.NewGuid(),
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            Currency: 1,
            Category: 4,
            StartingPrice: 2,
            CreatedAt: now,
            EndsAt: now.AddHours(24)));

        var result = await new AuctionHouseRepository().PlaceScalarBidAsync(new EntityDataContext(), new ScalarBidDraft(
            OperationId: Guid.NewGuid(),
            BidderAccountId: this._accountA,
            BidderCharacterId: Guid.NewGuid(),
            ListingId: jewel.ListingId,
            ExpectedVersion: 0,
            BidAmount: 2,
            MinimumBid: 2,
            BidderIp: "127.0.0.1",
            Now: now,
            NewEndsAt: now.AddHours(24)));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.UnsupportedCurrency));
        await using var context = new EntityDataContext();
        await context.Set<AuctionListing>().Where(l => l.Id == jewel.ListingId).ExecuteDeleteAsync();
    }

    private async Task SetMoney(Guid storage, int money)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE data.\"ItemStorage\" SET \"Money\" = @money WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("money", money);
        command.Parameters.AddWithValue("id", storage);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> GetMoney(Guid storage)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Money\" FROM data.\"ItemStorage\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", storage);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private Task<AuctionBidResult> Bid(Guid account, Guid storage, long amount, Guid operation)
    {
        var now = DateTimeOffset.UtcNow;
        return new AuctionHouseRepository().PlaceScalarBidAsync(new EntityDataContext(), new ScalarBidDraft(
            OperationId: operation,
            BidderAccountId: account,
            BidderCharacterId: this.CharacterFor(storage),
            ListingId: this._listingId,
            ExpectedVersion: this._version,
            BidAmount: amount,
            MinimumBid: StartingPrice,
            BidderIp: "127.0.0.1",
            Now: now,
            NewEndsAt: now.AddHours(24)));
    }

    private Task<AuctionBuyoutResult> Buy(Guid account, Guid storage, Guid operation)
    {
        return new AuctionHouseRepository().BuyoutAsync(new EntityDataContext(), new BuyoutDraft(
            OperationId: operation,
            BuyerAccountId: account,
            BuyerCharacterId: this.CharacterFor(storage),
            ListingId: this._listingId,
            ExpectedVersion: this._version,
            SuccessFee: SuccessFee,
            BuyerIp: "127.0.0.1",
            Now: DateTimeOffset.UtcNow));
    }

    private Guid CharacterFor(Guid storage)
    {
        using var connection = new NpgsqlConnection(this._connectionString);
        connection.Open();
        using var command = new NpgsqlCommand("SELECT \"Id\" FROM data.\"Character\" WHERE \"InventoryId\" = @storage", connection);
        command.Parameters.AddWithValue("storage", storage);
        return (Guid)command.ExecuteScalar()!;
    }
}
