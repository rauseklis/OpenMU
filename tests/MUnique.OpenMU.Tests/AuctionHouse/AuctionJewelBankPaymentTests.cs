// <copyright file="AuctionJewelBankPaymentTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.JewelBank;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using MUnique.OpenMU.Persistence.JewelBank;
using Npgsql;

/// <summary>
/// Verifies against PostgreSQL that the Auction House takes jewel and fruit payments out of the buyer's Jewel Bank (and never the
/// backpack), and that claimed proceeds and refunds go into the Jewel Bank. Requires <c>AUCTION_TEST_CONNECTION_STRING</c>.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionJewelBankPaymentTests
{
    private const long SaleFee = 100_000;
    private const int StartingMoney = 1_000_000;
    private static readonly byte Soul = AuctionBankWallet.JewelCode(3)!.Value;
    private static readonly byte Bless = AuctionBankWallet.JewelCode(2)!.Value;
    private static readonly byte StrengthFruit = AuctionBankWallet.FruitCode(FruitComponent.Strength);
    private readonly List<Guid> _listings = new();
    private string _connectionString = string.Empty;
    private Guid _sellerAccount;
    private Guid _buyer;
    private Guid _buyerCharacter;
    private Guid _buyerInventory;
    private Guid _other;
    private Guid _otherCharacter;

    /// <summary>
    /// Connects to the disposable database, or ignores the tests without one.
    /// </summary>
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

    /// <summary>
    /// Funds three characters and empties their banks.
    /// </summary>
    /// <returns>A task.</returns>
    [SetUp]
    public async Task Arrange()
    {
        var owners = await AuctionTestCharacters.FundOwnedAsync(this._connectionString, StartingMoney, 3);
        if (owners.Count < 3)
        {
            Assert.Ignore("The database has too few linked characters; use the populated copy.");
        }

        this._sellerAccount = owners[0].AccountId;
        (this._buyer, this._buyerCharacter) = owners[1];
        (this._other, this._otherCharacter) = owners[2];
        await using var context = new EntityDataContext();
        this._buyerInventory = (await context.Set<Character>().Where(c => c.Id == this._buyerCharacter).Select(c => c.InventoryId).SingleAsync())!.Value;
        await this.ClearBanksAsync();
    }

    /// <summary>
    /// Removes what the tests created.
    /// </summary>
    /// <returns>A task.</returns>
    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        var accounts = new[] { this._sellerAccount, this._buyer, this._other };
        await context.Set<AuctionCurrencyMovement>().Where(m => accounts.Contains(m.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => accounts.Contains(c.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionNotification>().Where(n => accounts.Contains(n.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionBid>().Where(b => this._listings.Contains(b.ListingId)).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => accounts.Contains(o.AccountId)).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => this._listings.Contains(l.Id) || l.SellerAccountId == this._sellerAccount).ExecuteDeleteAsync();
        this._listings.Clear();
        await this.ClearBanksAsync();
    }

    /// <summary>
    /// A bid takes every jewel, fruit and Zen of its basket: the jewels and fruits from the bank, the Zen from the inventory.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BidTakesJewelsAndFruitsFromTheBankAndZenFromTheInventory()
    {
        await this.SetBankAsync(this._buyer, (Soul, 5), (StrengthFruit, 15));
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.StrengthFruit, 10), (PriceKind.Zen, 50_000)));

        var result = await new AuctionHouseRepository().PlaceMixedBidAsync(new EntityDataContext(), this.Bid(listing, 0, (PriceKind.Soul, 3), (PriceKind.StrengthFruit, 10), (PriceKind.Zen, 50_000)));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.Placed));
        Assert.Multiple(async () =>
        {
            Assert.That(await this.BankAsync(this._buyer, Soul), Is.EqualTo(2));
            Assert.That(await this.BankAsync(this._buyer, StrengthFruit), Is.EqualTo(5));
            Assert.That(await this.MoneyAsync(this._buyerInventory), Is.EqualTo(StartingMoney - 50_000));
            Assert.That(await new JewelBankRepository().ReconcileAsync(), Is.Empty);
        });
    }

    /// <summary>
    /// A bank which holds too little rejects the bid and changes nothing, even for the components it could pay.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortBankRejectsTheBidAndChangesNothing()
    {
        await this.SetBankAsync(this._buyer, (Soul, 5), (StrengthFruit, 2));
        var listing = await this.InsertAsync(Basket((PriceKind.Soul, 3), (PriceKind.StrengthFruit, 10), (PriceKind.Zen, 50_000)));

        var result = await new AuctionHouseRepository().PlaceMixedBidAsync(new EntityDataContext(), this.Bid(listing, 0, (PriceKind.Soul, 3), (PriceKind.StrengthFruit, 10), (PriceKind.Zen, 50_000)));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBidOutcome.InsufficientCurrency));
        Assert.Multiple(async () =>
        {
            Assert.That(await this.BankAsync(this._buyer, Soul), Is.EqualTo(5));
            Assert.That(await this.BankAsync(this._buyer, StrengthFruit), Is.EqualTo(2));
            Assert.That(await this.MoneyAsync(this._buyerInventory), Is.EqualTo(StartingMoney));
        });
    }

    /// <summary>
    /// Jewels in the backpack do not pay: an empty bank rejects the purchase however many jewels the inventory holds.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackpackJewelsDoNotPay()
    {
        var soulDefinition = await this.DefinitionAsync(14, 14);
        await using (var context = new EntityDataContext())
        {
            for (var index = 0; index < 5; index++)
            {
                context.Set<Item>().Add(new Item { Id = Guid.NewGuid(), ItemStorageId = this._buyerInventory, ItemSlot = (byte)(70 + index), DefinitionId = soulDefinition, Level = 0, Durability = 1 });
            }

            await context.SaveChangesAsync();
        }

        try
        {
            var listing = await this.InsertAsync(start: null, buyout: Basket((PriceKind.Soul, 3)));

            var result = await new AuctionHouseRepository().PlaceMixedBuyoutAsync(new EntityDataContext(), this.Buyout(listing, 0));

            Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.InsufficientCurrency));
        }
        finally
        {
            await using var context = new EntityDataContext();
            await context.Set<Item>().Where(i => i.ItemStorageId == this._buyerInventory && i.DefinitionId == soulDefinition && i.ItemSlot >= 70 && i.ItemSlot < 75).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// A buyout paid from a bundle in the bank gives the surplus back as loose jewels, in the same transaction.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BuyoutBreaksABundleAndReturnsTheChangeToTheBank()
    {
        await this.SetBankAsync(this._buyer, (AuctionBankWallet.PackCode(Soul, 0), 1));
        var listing = await this.InsertAsync(start: null, buyout: Basket((PriceKind.Soul, 7)));

        var result = await new AuctionHouseRepository().PlaceMixedBuyoutAsync(new EntityDataContext(), this.Buyout(listing, 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        Assert.Multiple(async () =>
        {
            Assert.That(await this.BankAsync(this._buyer, AuctionBankWallet.PackCode(Soul, 0)), Is.EqualTo(0));
            Assert.That(await this.BankAsync(this._buyer, Soul), Is.EqualTo(3));
            Assert.That(await new JewelBankRepository().ReconcileAsync(), Is.Empty);
        });
    }

    /// <summary>
    /// A buyout leaves the seller's jewels in a Mailbox collection, and claiming that collection credits the seller's bank, not the inventory.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClaimingProceedsCreditsTheSellersBank()
    {
        await this.SetBankAsync(this._buyer, (Bless, 2), (StrengthFruit, 15));
        var listing = await this.InsertAsync(start: null, buyout: Basket((PriceKind.Bless, 2), (PriceKind.StrengthFruit, 15)));
        var bought = await new AuctionHouseRepository().PlaceMixedBuyoutAsync(new EntityDataContext(), this.Buyout(listing, 0));
        Assert.That(bought.Outcome, Is.EqualTo(AuctionBuyoutOutcome.Bought));
        await using var context = new EntityDataContext();
        var proceeds = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.AccountId == this._sellerAccount && c.Kind == 2);
        var sellerCharacter = (await AuctionTestCharacters.FundOwnedAsync(this._connectionString, StartingMoney, 3))[0].CharacterId;

        var claimed = await new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(Guid.NewGuid(), this._sellerAccount, sellerCharacter, proceeds.Id, proceeds.Version, DateTimeOffset.UtcNow));

        Assert.That(claimed.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
        Assert.Multiple(async () =>
        {
            Assert.That(await this.BankAsync(this._sellerAccount, Bless), Is.EqualTo(2));
            Assert.That(await this.BankAsync(this._sellerAccount, StrengthFruit), Is.EqualTo(15));
            Assert.That(await this.BankAsync(this._buyer, Bless), Is.EqualTo(0));
            Assert.That(await new JewelBankRepository().ReconcileAsync(), Is.Empty);
        });
    }

    /// <summary>
    /// A claim which would push a kind over the bank's limit changes nothing and leaves the collection claimable.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClaimOverTheBankLimitChangesNothing()
    {
        var limit = JewelBankLimits.MaximumPerKind;
        await this.SetBankAsync(this._other, (Soul, limit - 1));
        var collection = await this.InsertCollectionAsync(this._other, Basket((PriceKind.Soul, 3)));

        var claimed = await new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(Guid.NewGuid(), this._other, this._otherCharacter, collection.Id, collection.Version, DateTimeOffset.UtcNow));

        Assert.That(claimed.Outcome, Is.EqualTo(AuctionClaimOutcome.BankLimit));
        await using var context = new EntityDataContext();
        var stored = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.Id == collection.Id);
        Assert.Multiple(async () =>
        {
            Assert.That(await this.BankAsync(this._other, Soul), Is.EqualTo(limit - 1));
            Assert.That(stored.Status, Is.Not.EqualTo(2));
        });
    }

    private static PriceBasket Basket(params (PriceKind Kind, long Quantity)[] parts)
        => PriceBasket.FromComponents(parts.Select(p => new PriceComponent(p.Kind, p.Quantity)));

    private MixedBidDraft Bid(Guid listing, int version, params (PriceKind Kind, long Quantity)[] parts)
        => new(Guid.NewGuid(), this._buyer, this._buyerCharacter, listing, version, Basket(parts), "127.0.0.1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(24));

    private MixedBuyoutDraft Buyout(Guid listing, int version)
        => new(Guid.NewGuid(), this._buyer, this._buyerCharacter, listing, version, "127.0.0.1", DateTimeOffset.UtcNow);

    private Task<Guid> InsertAsync(PriceBasket start) => this.InsertAsync(start, null);

    private async Task<Guid> InsertAsync(PriceBasket? start, PriceBasket? buyout)
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        this._listings.Add(id);
        await using var context = new EntityDataContext();
        context.Set<AuctionListing>().Add(new AuctionListing
        {
            Id = id,
            ItemId = Guid.NewGuid(),
            SellerAccountId = this._sellerAccount,
            SellerCharacterId = Guid.NewGuid(),
            SellerName = "Seller",
            SellerIp = "10.0.0.1",
            Status = 0,
            Currency = (byte)AuctionCurrencyMode.Mixed,
            Category = 0,
            SubCategory = 255,
            PriceVector = start?.ToVectorText(),
            BuyoutVector = buyout?.ToVectorText(),
            KindMask = (start ?? PriceBasket.Empty).KindMask | (buyout ?? PriceBasket.Empty).KindMask,
            SaleFeeHeld = SaleFee,
            SaleFeeState = (byte)SaleFeeState.Held,
            CreatedAt = now,
            OriginalEndsAt = now.AddHours(24),
            EndsAt = now.AddHours(24),
            NormalizedName = string.Empty,
            Version = 0,
        });
        await context.SaveChangesAsync();
        return id;
    }

    private async Task<AuctionCollection> InsertCollectionAsync(Guid account, PriceBasket basket)
    {
        var now = DateTimeOffset.UtcNow;
        var collection = new AuctionCollection
        {
            Id = Guid.NewGuid(),
            AccountId = account,
            Kind = 2,
            Currency = (byte)AuctionCurrencyMode.Mixed,
            OriginalVector = basket.ToVectorText(),
            RemainingVector = basket.ToVectorText(),
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };
        await using var context = new EntityDataContext();
        context.Set<AuctionCollection>().Add(collection);
        await context.SaveChangesAsync();
        return collection;
    }

    private async Task ClearBanksAsync()
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM data.\"JewelBankMovement\" WHERE \"AccountId\" = ANY(@accounts); DELETE FROM data.\"JewelBankBalance\" WHERE \"AccountId\" = ANY(@accounts);", connection);
        command.Parameters.AddWithValue("accounts", new[] { this._sellerAccount, this._buyer, this._other });
        await command.ExecuteNonQueryAsync();
    }

    private async Task SetBankAsync(Guid account, params (byte Code, long Count)[] balances)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        foreach (var (code, count) in balances)
        {
            // The movement keeps the balance equal to the sum of the movements, as the reconciliation requires.
            await using var command = new NpgsqlCommand(
                "INSERT INTO data.\"JewelBankBalance\" (\"AccountId\", \"Kind\", \"Count\", \"UpdatedAt\") VALUES (@account, @kind, @count, now()) ON CONFLICT (\"AccountId\", \"Kind\") DO UPDATE SET \"Count\" = EXCLUDED.\"Count\";"
                + " INSERT INTO data.\"JewelBankMovement\" (\"Id\", \"AccountId\", \"CharacterId\", \"OperationId\", \"Kind\", \"Delta\", \"Balance\", \"Reason\", \"CreatedAt\") VALUES (gen_random_uuid(), @account, @character, gen_random_uuid(), @kind, @count, @count, 0, now());",
                connection);
            command.Parameters.AddWithValue("account", account);
            command.Parameters.AddWithValue("kind", (short)code);
            command.Parameters.AddWithValue("count", count);
            command.Parameters.AddWithValue("character", account == this._buyer ? this._buyerCharacter : this._otherCharacter);
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<long> BankAsync(Guid account, byte code)
        => (await new JewelBankRepository().GetStateAsync(account)).CountOf(code);

    private async Task<Guid> DefinitionAsync(int group, int number)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Id\" FROM config.\"ItemDefinition\" WHERE \"Group\" = @group AND \"Number\" = @number", connection);
        command.Parameters.AddWithValue("group", (short)group);
        command.Parameters.AddWithValue("number", (short)number);
        var found = await command.ExecuteScalarAsync();
        if (found is null)
        {
            Assert.Ignore($"The database has no item {group}/{number}; use the populated copy.");
        }

        return (Guid)found!;
    }

    private async Task<int> MoneyAsync(Guid inventory)
    {
        await using var connection = new NpgsqlConnection(this._connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Money\" FROM data.\"ItemStorage\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", inventory);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
