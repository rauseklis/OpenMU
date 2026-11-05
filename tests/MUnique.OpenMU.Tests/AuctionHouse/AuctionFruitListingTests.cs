// <copyright file="AuctionFruitListingTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using MUnique.OpenMU.Persistence.JewelBank;
using Npgsql;

/// <summary>
/// Verifies listing a fruit basket against PostgreSQL: the per-component fee is paid from clean fruits at each component's level,
/// the starting basket is stored on the listing, and a basket the seller cannot pay for changes nothing.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionFruitListingTests
{
    private static readonly byte StrengthFruit = AuctionBankWallet.FruitCode(FruitComponent.Strength);
    private static readonly byte EnergyFruit = AuctionBankWallet.FruitCode(FruitComponent.Energy);
    private string _connectionString = string.Empty;
    private Guid _accountId;
    private Guid _characterId;
    private Guid _inventoryId;
    private Guid _listedItemId;
    private Guid _listingId;
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
        await using var context = new EntityDataContext();
        this._inventoryId = (await context.Set<Character>().AsNoTracking().Where(c => c.Id == this._characterId).Select(c => c.InventoryId).SingleAsync())!.Value;
        await AuctionTestBank.ClearAsync(this._connectionString, this._accountId);
        var listedDefinition = await context.Set<Item>()
            .Where(i => i.DefinitionId != null)
            .Select(i => i.DefinitionId!.Value)
            .FirstAsync();

        // Against a populated copy of the real database, slots 70-73 of this inventory may already hold
        // real, unrelated items; clear them so these fixed test slots are deterministic.
        await context.Set<Item>().Where(i => i.ItemStorageId == this._inventoryId && i.ItemSlot >= 70 && i.ItemSlot <= 73).ExecuteDeleteAsync();
        this._listedItemId = Guid.NewGuid();
        context.Set<Item>().Add(new Item { Id = this._listedItemId, ItemStorageId = this._inventoryId, ItemSlot = 73, DefinitionId = listedDefinition, Durability = 10 });
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionCurrencyMovement>().Where(m => m.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.SellerAccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<Item>().Where(i => i.Id == this._listedItemId).ExecuteDeleteAsync();
        await AuctionTestBank.ClearAsync(this._connectionString, this._accountId);

        await context.Set<AuctionCollection>().Where(c => c.AccountId == this._accountId).ExecuteDeleteAsync();
        this._collections.Clear();
    }

    /// <summary>
    /// Verifies a fruit basket pays one fruit of each nonzero component's fee from the fruit at that component's level, stores the
    /// starting basket on the listing, and records one fee movement per component.
    /// </summary>
    [Test]
    public async Task BasketFeeIsPaidPerComponentFromTheMatchingFruit()
    {
        await this.AddFruitAsync(level: 3, slot: 70);
        await this.AddFruitAsync(level: 3, slot: 71);
        await this.AddFruitAsync(level: 0, slot: 72);

        var result = await this.ListAsync(new FruitBasket(Strength: 2, Agility: 0, Vitality: 0, Energy: 1, Command: 0));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        await using var context = new EntityDataContext();
        var strengthLeft = await AuctionTestBank.CountAsync(this._accountId, StrengthFruit);
        var energyLeft = await AuctionTestBank.CountAsync(this._accountId, EnergyFruit);
        var listing = await context.Set<AuctionListing>().AsNoTracking().SingleAsync(l => l.Id == this._listingId);
        var feeRows = await context.Set<AuctionCurrencyMovement>().AsNoTracking().Where(m => m.OperationId == this._lastOperation && m.Kind == 5).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(strengthLeft, Is.EqualTo(1));
            Assert.That(energyLeft, Is.EqualTo(0));
            Assert.That(listing.StartingStrength, Is.EqualTo(2));
            Assert.That(listing.StartingEnergy, Is.EqualTo(1));
            Assert.That(feeRows.Select(m => m.FruitComponent).OrderBy(c => c), Is.EquivalentTo(new byte?[] { 0, 3 }));
            Assert.That(feeRows.All(m => m.Currency == 8 && m.Amount == 1), Is.True);
        });
    }

    /// <summary>
    /// Verifies a basket the seller cannot pay for is refused, and no fruit is consumed.
    /// </summary>
    [Test]
    public async Task BasketWithoutTheFruitsIsRefused()
    {
        await this.AddFruitAsync(level: 3, slot: 70);
        await this.AddFruitAsync(level: 3, slot: 71);

        var result = await this.ListAsync(new FruitBasket(Strength: 2, Agility: 0, Vitality: 0, Energy: 1, Command: 0));

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.InsufficientCurrency));
        Assert.That(await AuctionTestBank.CountAsync(this._accountId, StrengthFruit), Is.EqualTo(2));
    }

    /// <summary>
    /// Verifies fruit price ranges compare every component independently instead of inventing a basket total.
    /// </summary>
    [Test]
    public async Task BasketSearchFiltersEachComponent()
    {
        await this.AddFruitAsync(level: 3, slot: 70);
        await this.AddFruitAsync(level: 3, slot: 71);
        await this.AddFruitAsync(level: 0, slot: 72);
        var result = await this.ListAsync(new FruitBasket(Strength: 2, Agility: 0, Vitality: 0, Energy: 1, Command: 0));
        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
        var repository = new AuctionHouseRepository();

        var minimumMatch = await repository.SearchListingsAsync(new EntityDataContext(), new AuctionListingSearch(
            Currency: 8,
            MinimumFruits: new FruitBasket(2, 0, 0, 1, 0)));
        var minimumMiss = await repository.SearchListingsAsync(new EntityDataContext(), new AuctionListingSearch(
            Currency: 8,
            MinimumFruits: new FruitBasket(3, 0, 0, 1, 0)));
        var maximumMatch = await repository.SearchListingsAsync(new EntityDataContext(), new AuctionListingSearch(
            Currency: 8,
            MaximumFruits: new FruitBasket(2, 0, 0, 1, 0)));
        var maximumMiss = await repository.SearchListingsAsync(new EntityDataContext(), new AuctionListingSearch(
            Currency: 8,
            MaximumFruits: new FruitBasket(1, 0, 0, 1, 0)));

        Assert.Multiple(() =>
        {
            Assert.That(minimumMatch.Rows.Any(r => r.ListingId == this._listingId), Is.True);
            Assert.That(minimumMiss.Rows.Any(r => r.ListingId == this._listingId), Is.False);
            Assert.That(maximumMatch.Rows.Any(r => r.ListingId == this._listingId), Is.True);
            Assert.That(maximumMiss.Rows.Any(r => r.ListingId == this._listingId), Is.False);
        });
    }

    /// <summary>
    /// Verifies a claimed fruit balance is credited to the Jewel Bank: two Strength fruits and one Energy fruit.
    /// </summary>
    [Test]
    public async Task ClaimingAFruitBalanceCreditsTheJewelBank()
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
                Currency = 8,
                OriginalStrength = 2,
                RemainingStrength = 2,
                OriginalAgility = 0,
                RemainingAgility = 0,
                OriginalVitality = 0,
                RemainingVitality = 0,
                OriginalEnergy = 1,
                RemainingEnergy = 1,
                OriginalCommand = 0,
                RemainingCommand = 0,
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
        var level3 = await AuctionTestBank.CountAsync(this._accountId, StrengthFruit);
        var level0 = await AuctionTestBank.CountAsync(this._accountId, EnergyFruit);
        Assert.Multiple(() =>
        {
            Assert.That(level3, Is.EqualTo(2));
            Assert.That(level0, Is.EqualTo(1));
        });
    }

    private Guid _lastOperation;

    private Task AddFruitAsync(byte level, byte slot)
        => AuctionTestBank.AddAsync(this._connectionString, this._accountId, this._characterId, (byte)(JewelBankKinds.FirstFruitCode + level), 1);

    private async Task<AuctionListItemResult> ListAsync(FruitBasket basket)
    {
        this._lastOperation = Guid.NewGuid();
        var service = new AuctionHouseService(new AuctionHouseRepository(), new AuctionHouseOptions { Enabled = true });
        var result = await service.ListItemAsync(new AuctionListItemRequest(
            OperationId: this._lastOperation,
            AccountId: this._accountId,
            CharacterId: this._characterId,
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            InventorySlot: 73,
            Currency: 8,
            Category: 0,
            StartingPrice: 0,
            BuyoutPrice: null,
            DurationHours: 24,
            StartingFruits: basket));
        this._listingId = result.ListingId;
        return result;
    }
}
