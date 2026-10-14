// <copyright file="AuctionSaleFeeTests.cs" company="MUnique">
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
/// Verifies the flat 100,000 Zen sale tax against PostgreSQL: it is held when the listing is created, refunded through the
/// Mailbox when the item does not sell (cancel, expiry without a bid, administrative removal), and consumed when it sells.
/// No percentage fee is charged. Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionSaleFeeTests
{
    private const long Fee = 100_000;
    private const int StartingMoney = 10_000_000;
    private const byte FeeRemovedKind = 5;
    private const byte FeeRefundCollectionKind = 6;
    private string _connectionString = string.Empty;
    private Guid _sellerAccount;
    private Guid _sellerCharacter;
    private Guid _sellerInventory;
    private Guid _buyerAccount;
    private Guid _buyerCharacter;
    private Guid _itemId;
    private byte _slot;
    private Guid _lastOperation;

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
        var owners = await AuctionTestCharacters.FundOwnedAsync(this._connectionString, StartingMoney, 2);
        if (owners.Count < 2)
        {
            Assert.Ignore("The database needs two accounts with a character; use the populated copy.");
        }

        (this._sellerAccount, this._sellerCharacter) = owners[0];
        (this._buyerAccount, this._buyerCharacter) = owners[1];
        await using var context = new EntityDataContext();
        this._sellerInventory = (await context.Set<Character>().Where(c => c.Id == this._sellerCharacter).Select(c => c.InventoryId).SingleAsync())!.Value;
        var definitionId = await context.Set<Item>()
            .Where(i => i.DefinitionId != null && i.RawDefinition.IsTradable && i.RawDefinition.IsPersonalStoreSellable && !i.RawDefinition.IsBoundToCharacter && !i.RawDefinition.IsQuestItem)
            .Select(i => i.DefinitionId)
            .FirstOrDefaultAsync();
        if (definitionId is null)
        {
            Assert.Ignore("The database has no sellable item definition; use the populated copy.");
        }

        var used = await context.Set<Item>().AsNoTracking().Where(i => i.ItemStorageId == this._sellerInventory).Select(i => i.ItemSlot).ToListAsync();
        this._slot = (byte)Enumerable.Range(InventoryConstants.EquippableSlotsCount, 64).First(slot => !used.Contains((byte)slot));
        this._itemId = Guid.NewGuid();
        context.Set<Item>().Add(new Item
        {
            Id = this._itemId,
            ItemStorageId = this._sellerInventory,
            ItemSlot = this._slot,
            DefinitionId = definitionId,
            Durability = 10,
            Level = 3,
        });
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        var accounts = new[] { this._sellerAccount, this._buyerAccount };
        await context.Set<AuctionCurrencyMovement>().Where(m => accounts.Contains(m.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => accounts.Contains(o.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionNotification>().Where(n => accounts.Contains(n.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => accounts.Contains(c.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => accounts.Contains(b.BidderAccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.SellerAccountId == this._sellerAccount).ExecuteDeleteAsync();
        await context.Set<Item>().Where(i => i.Id == this._itemId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies the flat fee is taken from the seller's Zen at listing time and recorded as held.
    /// </summary>
    [Test]
    public async Task ListingHoldsTheFlatFee()
    {
        var result = await this.ListAsync();

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        await using var context = new EntityDataContext();
        var listing = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == result.ListingId);
        var movement = await context.Set<AuctionCurrencyMovement>().AsNoTracking().SingleAsync(m => m.OperationId == this._lastOperation);
        Assert.Multiple(async () =>
        {
            Assert.That(listing.SaleFeeHeld, Is.EqualTo(Fee));
            Assert.That(listing.SaleFeeState, Is.EqualTo((byte)SaleFeeState.Held));
            Assert.That(movement.Kind, Is.EqualTo(FeeRemovedKind));
            Assert.That(movement.Amount, Is.EqualTo(Fee));
            Assert.That(await this.MoneyAsync(this._sellerInventory), Is.EqualTo(StartingMoney - Fee));
        });
    }

    /// <summary>
    /// Verifies a seller who cannot pay the fee is refused and keeps the item and the Zen.
    /// </summary>
    [Test]
    public async Task ListingWithoutEnoughZenForTheFeeIsRefused()
    {
        await this.SetMoneyAsync(this._sellerInventory, 50_000);

        var result = await this.ListAsync();

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.InsufficientCurrency));
        await using var context = new EntityDataContext();
        var item = await context.Set<Item>().AsNoTracking().SingleAsync(i => i.Id == this._itemId);
        Assert.Multiple(async () =>
        {
            Assert.That(item.ItemStorageId, Is.EqualTo(this._sellerInventory));
            Assert.That(await this.MoneyAsync(this._sellerInventory), Is.EqualTo(50_000));
        });
    }

    /// <summary>
    /// Verifies no fee is held when the tax is configured off.
    /// </summary>
    [Test]
    public async Task ZeroFeeHoldsNothing()
    {
        var result = await this.ListAsync(fee: 0);

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        await using var context = new EntityDataContext();
        var listing = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == result.ListingId);
        Assert.Multiple(async () =>
        {
            Assert.That(listing.SaleFeeHeld, Is.Zero);
            Assert.That(listing.SaleFeeState, Is.EqualTo((byte)SaleFeeState.None));
            Assert.That(await this.MoneyAsync(this._sellerInventory), Is.EqualTo(StartingMoney));
        });
    }

    /// <summary>
    /// Verifies cancelling refunds the fee through the Mailbox, and claiming it gives the Zen back.
    /// </summary>
    [Test]
    public async Task CancelRefundsTheFeeThroughTheMailbox()
    {
        var created = await this.ListAsync();
        var listing = await this.LoadListingAsync(created.ListingId);

        var cancelled = await new AuctionHouseRepository().CancelAsync(
            new EntityDataContext(),
            new CancelDraft(Guid.NewGuid(), this._sellerAccount, created.ListingId, listing.Version, DateTimeOffset.UtcNow));

        Assert.That(cancelled.Outcome, Is.EqualTo(AuctionCancelOutcome.Cancelled));
        await this.AssertFeeRefundedAndClaimableAsync(created.ListingId);
    }

    /// <summary>
    /// Verifies an auction that ends without a bid refunds the fee.
    /// </summary>
    [Test]
    public async Task ExpiryWithoutABidRefundsTheFee()
    {
        var created = await this.ListAsync();
        await using (var context = new EntityDataContext())
        {
            await context.Set<AuctionListing>().Where(l => l.Id == created.ListingId)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.EndsAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var expired = await new AuctionHouseRepository().ExpireAsync(new EntityDataContext(), new ExpireDraft(created.ListingId, 0, DateTimeOffset.UtcNow));

        Assert.That(expired.Outcome, Is.EqualTo(AuctionExpireOutcome.Finalized));
        await this.AssertFeeRefundedAndClaimableAsync(created.ListingId);
    }

    /// <summary>
    /// Verifies an administrative removal refunds the fee.
    /// </summary>
    [Test]
    public async Task AdminRemovalRefundsTheFee()
    {
        var created = await this.ListAsync();

        var removed = await new AuctionHouseRepository().AdminRemoveAsync(
            new EntityDataContext(),
            new AdminRemoveDraft(created.ListingId, "admin", "test", DateTimeOffset.UtcNow));

        Assert.That(removed, Is.EqualTo(AdminRemoveOutcome.Removed));
        await this.AssertFeeRefundedAndClaimableAsync(created.ListingId);
    }

    /// <summary>
    /// Verifies a sale consumes the fee: nothing is refunded and the seller's Zen stays down by the fee.
    /// </summary>
    [Test]
    public async Task ABuyoutConsumesTheFee()
    {
        var created = await this.ListAsync(buyoutPrice: 6_000);
        var listing = await this.LoadListingAsync(created.ListingId);

        var bought = await new AuctionHouseRepository().BuyoutAsync(
            new EntityDataContext(),
            new BuyoutDraft(Guid.NewGuid(), this._buyerAccount, this._buyerCharacter, created.ListingId, listing.Version, 0, "127.0.0.2", DateTimeOffset.UtcNow));

        Assert.That(bought.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        await using var context = new EntityDataContext();
        var after = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == created.ListingId);
        var refunds = await context.Set<AuctionCollection>().AsNoTracking().CountAsync(c => c.ListingId == created.ListingId && c.Kind == FeeRefundCollectionKind);
        Assert.Multiple(async () =>
        {
            Assert.That(after.SaleFeeState, Is.EqualTo((byte)SaleFeeState.Consumed));
            Assert.That(refunds, Is.Zero);
            Assert.That(await this.MoneyAsync(this._sellerInventory), Is.EqualTo(StartingMoney - Fee));
        });
    }

    private async Task AssertFeeRefundedAndClaimableAsync(Guid listingId)
    {
        await using var context = new EntityDataContext();
        var listing = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == listingId);
        var refund = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.ListingId == listingId && c.Kind == FeeRefundCollectionKind);
        Assert.Multiple(() =>
        {
            Assert.That(listing.SaleFeeState, Is.EqualTo((byte)SaleFeeState.Refunded));
            Assert.That(refund.AccountId, Is.EqualTo(this._sellerAccount));
            Assert.That(refund.Currency, Is.Zero);
            Assert.That(refund.RemainingScalar, Is.EqualTo(Fee));
        });

        var claimed = await new AuctionHouseRepository().ClaimAsync(
            new EntityDataContext(),
            new ClaimDraft(Guid.NewGuid(), this._sellerAccount, this._sellerCharacter, refund.Id, refund.Version, DateTimeOffset.UtcNow));
        Assert.That(claimed.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
        Assert.That(await this.MoneyAsync(this._sellerInventory), Is.EqualTo(StartingMoney));
    }

    private async Task<AuctionListItemResult> ListAsync(long fee = Fee, long? buyoutPrice = null)
    {
        this._lastOperation = Guid.NewGuid();
        var service = new AuctionHouseService(new AuctionHouseRepository(), new AuctionHouseOptions
        {
            Enabled = true,
            SaleFee = new SaleFee(SaleFeeCurrency.Zen, fee),
            ListingFeeBasisPoints = 0,
            SuccessFeeBasisPoints = 0,
        });
        return await service.ListItemAsync(new AuctionListItemRequest(
            OperationId: this._lastOperation,
            AccountId: this._sellerAccount,
            CharacterId: this._sellerCharacter,
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            InventorySlot: this._slot,
            Currency: 0,
            Category: 0,
            StartingPrice: 5_000,
            BuyoutPrice: buyoutPrice,
            DurationHours: 24));
    }

    private async Task<AuctionListing> LoadListingAsync(Guid listingId)
    {
        await using var context = new EntityDataContext();
        return await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == listingId);
    }

    private async Task SetMoneyAsync(Guid inventory, int money)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE data.\"ItemStorage\" SET \"Money\" = @money WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("money", money);
        command.Parameters.AddWithValue("id", inventory);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> MoneyAsync(Guid inventory)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Money\" FROM data.\"ItemStorage\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", inventory);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
