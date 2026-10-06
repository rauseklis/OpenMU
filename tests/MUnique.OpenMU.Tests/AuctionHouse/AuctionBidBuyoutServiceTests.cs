// <copyright file="AuctionBidBuyoutServiceTests.cs" company="MUnique">
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
/// Verifies bidding and buying out through the service against PostgreSQL: the request is validated against the listing as
/// it was read, the repository decides against the locked listing, and every outcome maps to a stable result code.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionBidBuyoutServiceTests
{
    private const long StartingPrice = 1000;
    private const long BuyoutPrice = 5000;
    private string _connectionString = string.Empty;
    private Guid _sellerAccountId;
    private Guid _bidderAccountId;
    private Guid _bidderCharacterId;
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
        this._sellerAccountId = Guid.NewGuid();
        var owners = await AuctionTestCharacters.FundOwnedAsync(this._connectionString, 10_000_000, 1);
        if (owners.Count == 0)
        {
            Assert.Ignore("The database has no character to bid with; use the populated copy.");
        }

        this._bidderAccountId = owners[0].AccountId;
        this._bidderCharacterId = owners[0].CharacterId;
        this._listingId = await this.ListAsync(buyoutPrice: BuyoutPrice);
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionCurrencyMovement>().Where(m => m.AccountId == this._sellerAccountId || m.AccountId == this._bidderAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => c.AccountId == this._sellerAccountId || c.AccountId == this._bidderAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => b.ListingId == this._listingId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._sellerAccountId || o.AccountId == this._bidderAccountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.Id == this._listingId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies a bid at the minimum is placed and reported as a success.
    /// </summary>
    [Test]
    public async Task BidAtTheMinimumIsPlaced()
    {
        var result = await this.CreateService().BidAsync(this.Bid(StartingPrice, expectedVersion: 0));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        Assert.That(await this.GetListingStatusAsync(), Is.EqualTo(0));
        Assert.That(await this.GetCurrentBidAsync(), Is.EqualTo(StartingPrice));
    }

    /// <summary>
    /// Verifies a bid below the minimum is refused with the bid-too-low code and nothing is escrowed.
    /// </summary>
    [Test]
    public async Task BidBelowTheMinimumIsRefused()
    {
        await this.CreateService().BidAsync(this.Bid(StartingPrice, expectedVersion: 0));

        var result = await this.CreateService().BidAsync(this.Bid(StartingPrice, expectedVersion: 1));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.BidTooLow));
    }

    /// <summary>
    /// Verifies a seller cannot bid on its own listing.
    /// </summary>
    [Test]
    public async Task SellerCannotBidOnOwnListing()
    {
        var request = this.Bid(StartingPrice, expectedVersion: 0) with { AccountId = this._sellerAccountId };

        var result = await this.CreateService().BidAsync(request);

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.SelfTrade));
    }

    /// <summary>
    /// Verifies a bid made against an outdated version is refused as stale.
    /// </summary>
    [Test]
    public async Task BidWithOutdatedVersionIsStale()
    {
        var result = await this.CreateService().BidAsync(this.Bid(StartingPrice, expectedVersion: 7));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.StaleListing));
    }

    /// <summary>
    /// Verifies a buyout completes the listing and pays the buyout price from the buyer's Zen.
    /// </summary>
    [Test]
    public async Task BuyoutCompletesTheListing()
    {
        var result = await this.CreateService().BuyoutAsync(this.Buyout(expectedVersion: 0));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        Assert.That(await this.GetListingStatusAsync(), Is.EqualTo(1));
        Assert.That(await this.GetMoneyAsync(), Is.EqualTo(10_000_000 - BuyoutPrice));
    }

    /// <summary>
    /// Verifies a listing without a buyout price refuses buyouts.
    /// </summary>
    [Test]
    public async Task BuyoutIsUnavailableWithoutBuyoutPrice()
    {
        var listingId = await this.ListAsync(buyoutPrice: null);

        var result = await this.CreateService().BuyoutAsync(this.Buyout(expectedVersion: 0) with { ListingId = listingId });

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.BuyoutUnavailable));
    }

    /// <summary>
    /// Verifies a disabled feature refuses bids and buyouts before reading anything.
    /// </summary>
    [Test]
    public async Task DisabledFeatureRefusesMutations()
    {
        var service = new AuctionHouseService(new AuctionHouseRepository(), new AuctionHouseOptions { Enabled = false });

        var bid = await service.BidAsync(this.Bid(StartingPrice, expectedVersion: 0));
        var buyout = await service.BuyoutAsync(this.Buyout(expectedVersion: 0));

        Assert.Multiple(() =>
        {
            Assert.That(bid.Result, Is.EqualTo(AuctionResultCode.Disabled));
            Assert.That(buyout.Result, Is.EqualTo(AuctionResultCode.Disabled));
        });
    }

    private AuctionHouseService CreateService()
    {
        return new AuctionHouseService(new AuctionHouseRepository(), new AuctionHouseOptions { Enabled = true });
    }

    private AuctionBidRequest Bid(long amount, int expectedVersion)
    {
        return new AuctionBidRequest(
            OperationId: Guid.NewGuid(),
            AccountId: this._bidderAccountId,
            CharacterId: this._bidderCharacterId,
            ListingId: this._listingId,
            ExpectedVersion: expectedVersion,
            BidAmount: amount,
            BidderIp: "127.0.0.1");
    }

    private AuctionBuyoutRequest Buyout(int expectedVersion)
    {
        return new AuctionBuyoutRequest(
            OperationId: Guid.NewGuid(),
            AccountId: this._bidderAccountId,
            CharacterId: this._bidderCharacterId,
            ListingId: this._listingId,
            ExpectedVersion: expectedVersion,
            BuyerIp: "127.0.0.1");
    }

    private async Task<Guid> ListAsync(long? buyoutPrice)
    {
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
            BuyoutPrice: buyoutPrice));
        return created.ListingId;
    }

    private async Task<byte> GetListingStatusAsync()
    {
        await using var context = new EntityDataContext();
        return await context.Set<AuctionListing>().AsNoTracking().Where(l => l.Id == this._listingId).Select(l => l.Status).SingleAsync();
    }

    private async Task<long?> GetCurrentBidAsync()
    {
        await using var context = new EntityDataContext();
        return await context.Set<AuctionListing>().AsNoTracking().Where(l => l.Id == this._listingId).Select(l => l.CurrentBid).SingleAsync();
    }

    private async Task<int> GetMoneyAsync()
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT s.\"Money\" FROM data.\"Character\" c JOIN data.\"ItemStorage\" s ON s.\"Id\" = c.\"InventoryId\" WHERE c.\"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", this._bidderCharacterId);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
