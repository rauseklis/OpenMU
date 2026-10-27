// <copyright file="CashShopRepositoryTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.CashShop;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.CashShop;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.CashShop;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using MUnique.OpenMU.Tests.AuctionHouse;

/// <summary>Runs real transactions against a fresh disposable PostgreSQL database, never production data.</summary>
[TestFixture]
[Category("PostgreSql")]
[NonParallelizable]
public class CashShopRepositoryTests
{
    private Guid _accountId;
    private Guid _otherAccountId;
    private readonly CashShopRepository _repository = new();

    /// <summary>Requires an explicitly supplied disposable database and applies real migrations.</summary>
    [OneTimeSetUp]
    public async Task Initialize()
    {
        var connection = AuctionPostgresTestEnvironment.ConnectionString;
        if (connection is null)
        {
            Assert.Ignore("Set AUCTION_TEST_CONNECTION_STRING to a disposable Cash Shop test database.");
        }

        var settings = new Npgsql.NpgsqlConnectionStringBuilder(connection!);
        if (settings.Database is null || !settings.Database.StartsWith("cash_shop_ci", StringComparison.Ordinal)
            || settings.Host is not ("localhost" or "127.0.0.1") || settings.Port != 55432)
        {
            throw new InvalidOperationException("Cash Shop tests require a loopback cash_shop_ci database on port 55432.");
        }

        AuctionPostgresTestEnvironment.EnsureConfigured();
        await using var context = new EntityDataContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>Each test creates its own isolated account records.</summary>
    [SetUp]
    public async Task Arrange()
    {
        this._accountId = Guid.NewGuid();
        this._otherAccountId = Guid.NewGuid();
        await using var context = new EntityDataContext();
        context.Set<Account>().AddRange(
            new Account { Id = this._accountId, LoginName = "cs" + this._accountId.ToString("N")[..8] },
            new Account { Id = this._otherAccountId, LoginName = "cs" + this._otherAccountId.ToString("N")[..8] });
        await context.SaveChangesAsync();
    }

    /// <summary>Deletes only the accounts created by this test; their wallet rows cascade.</summary>
    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<Account>().Where(a => a.Id == this._accountId || a.Id == this._otherAccountId).ExecuteDeleteAsync();
    }

    /// <summary>Missing wallets read as zero and credits survive new repository contexts.</summary>
    [Test]
    public async Task CreditsPersistAcrossReloadsAndStayIsolated()
    {
        Assert.That(await this._repository.GetBalanceAsync(this._accountId, 0), Is.EqualTo(new CashShopBalance()));
        var expected = new CashShopBalance(10_000, 10_000, 10_000);
        Assert.That(await this._repository.CreditAsync(this._accountId, 0, Guid.NewGuid(), expected, "test credit"), Is.EqualTo(expected));
        Assert.That(await new CashShopRepository().GetBalanceAsync(this._accountId, 0), Is.EqualTo(expected));
        Assert.That(await this._repository.GetBalanceAsync(this._accountId, 1), Is.EqualTo(new CashShopBalance()));
        Assert.That(await this._repository.GetBalanceAsync(this._otherAccountId, 0), Is.EqualTo(new CashShopBalance()));
    }

    /// <summary>Concurrent retries of one operation credit only once.</summary>
    [Test]
    public async Task SameOperationIsAppliedOnlyOnce()
    {
        var operation = Guid.NewGuid();
        var credit = new CashShopBalance(100, 200, 300);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            this._repository.CreditAsync(this._accountId, 0, operation, credit, "retry test")));
        Assert.That(await this._repository.GetBalanceAsync(this._accountId, 0), Is.EqualTo(credit));
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await this._repository.CreditAsync(this._accountId, 0, operation, new CashShopBalance(999), "retry test"));
        Assert.That(await this._repository.GetBalanceAsync(this._accountId, 0), Is.EqualTo(credit));
    }

    /// <summary>Independent concurrent operations accumulate without lost updates.</summary>
    [Test]
    public async Task ConcurrentCreditsAccumulate()
    {
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            this._repository.CreditAsync(this._accountId, 0, Guid.NewGuid(), new CashShopBalance(1, 2, 3), "concurrent test")));
        Assert.That(await this._repository.GetBalanceAsync(this._accountId, 0), Is.EqualTo(new CashShopBalance(12, 24, 36)));
    }

    /// <summary>Overflow rolls back every currency and leaves the operation available for a valid retry.</summary>
    [Test]
    public async Task OverflowRollsBackAllCurrencies()
    {
        await this._repository.CreditAsync(this._accountId, 0, Guid.NewGuid(),
            new CashShopBalance(CashShopBalance.Maximum, 10, 20), "cap setup");
        var operation = Guid.NewGuid();
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await this._repository.CreditAsync(this._accountId, 0, operation, new CashShopBalance(1, 100, 200), "overflow test"));
        Assert.That(await this._repository.GetBalanceAsync(this._accountId, 0), Is.EqualTo(new CashShopBalance(CashShopBalance.Maximum, 10, 20)));
        await this._repository.CreditAsync(this._accountId, 0, operation, new CashShopBalance(0, 1, 2), "valid retry");
        Assert.That(await this._repository.GetBalanceAsync(this._accountId, 0), Is.EqualTo(new CashShopBalance(CashShopBalance.Maximum, 11, 22)));
    }

    /// <summary>Negative amounts cannot turn an administrative credit into a withdrawal.</summary>
    [Test]
    public void NegativeCreditIsRejected()
        => Assert.ThrowsAsync<ArgumentException>(async () =>
            await this._repository.CreditAsync(this._accountId, 0, Guid.NewGuid(), new CashShopBalance(-1, 2, 3), "invalid"));
    /// <summary>A purchase debits exactly the price in its currency and stores the item; it never goes below zero.</summary>
    [Test]
    public async Task PurchaseDebitsOneCurrencyAndStoresTheItem()
    {
        await this._repository.CreditAsync(this._accountId, 0, Guid.NewGuid(), new CashShopBalance(100, 50, 30), "purchase setup");
        var item = new CashShopStorageItem(0, 122, 7, 9, 13, 44, 0, 1);

        Assert.That(await this._repository.PurchaseAsync(this._accountId, 0, CashShopCurrency.WCoinP, 20, item), Is.EqualTo(CashShopPurchaseResult.Success));
        Assert.That(await this._repository.GetBalanceAsync(this._accountId, 0), Is.EqualTo(new CashShopBalance(100, 30, 30)));
        Assert.That(await this._repository.PurchaseAsync(this._accountId, 0, CashShopCurrency.WCoinP, 31, item), Is.EqualTo(CashShopPurchaseResult.InsufficientFunds));
        Assert.That(await this._repository.PurchaseAsync(this._accountId, 1, CashShopCurrency.WCoinC, 1, item), Is.EqualTo(CashShopPurchaseResult.InsufficientFunds), "other server");

        var stored = await this._repository.ListStorageAsync(this._accountId, 0);
        Assert.That(stored, Has.Count.EqualTo(1));
        Assert.That(stored[0] with { Id = 0 }, Is.EqualTo(item));
        Assert.That(await this._repository.ListStorageAsync(this._otherAccountId, 0), Is.Empty);
    }

    /// <summary>Parallel purchases cannot spend more than the balance.</summary>
    [Test]
    public async Task ConcurrentPurchasesCannotOverspend()
    {
        await this._repository.CreditAsync(this._accountId, 0, Guid.NewGuid(), new CashShopBalance(100, 0, 0), "overspend setup");
        var item = new CashShopStorageItem(0, 122, 7, 9, 13, 44, 0, 1);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            this._repository.PurchaseAsync(this._accountId, 0, CashShopCurrency.WCoinC, 30, item)));

        Assert.That(results.Count(r => r == CashShopPurchaseResult.Success), Is.EqualTo(3));
        Assert.That(await this._repository.GetBalanceAsync(this._accountId, 0), Is.EqualTo(new CashShopBalance(10, 0, 0)));
        Assert.That(await this._repository.ListStorageAsync(this._accountId, 0), Has.Count.EqualTo(3));
    }

    /// <summary>The storage is capped, and a full storage costs nothing.</summary>
    [Test]
    public async Task FullStorageRefusesAndKeepsTheBalance()
    {
        await this._repository.CreditAsync(this._accountId, 0, Guid.NewGuid(), new CashShopBalance(1_000_000, 0, 0), "capacity setup");
        var item = new CashShopStorageItem(0, 122, 7, 9, 13, 44, 0, 1);
        for (var i = 0; i < CashShopRepository.MaximumStorageItems; i++)
        {
            Assert.That(await this._repository.PurchaseAsync(this._accountId, 0, CashShopCurrency.WCoinC, 1, item), Is.EqualTo(CashShopPurchaseResult.Success));
        }

        Assert.That(await this._repository.PurchaseAsync(this._accountId, 0, CashShopCurrency.WCoinC, 1, item), Is.EqualTo(CashShopPurchaseResult.StorageFull));
        Assert.That((await this._repository.GetBalanceAsync(this._accountId, 0)).WCoinC, Is.EqualTo(1_000_000 - CashShopRepository.MaximumStorageItems));
    }

    /// <summary>An item can be taken once only; another account cannot take it; a failed delivery can restore it.</summary>
    [Test]
    public async Task TakeIsExclusiveAndRestoreKeepsTheIdentifier()
    {
        await this._repository.CreditAsync(this._accountId, 0, Guid.NewGuid(), new CashShopBalance(10, 0, 0), "take setup");
        await this._repository.PurchaseAsync(this._accountId, 0, CashShopCurrency.WCoinC, 5, new CashShopStorageItem(0, 122, 7, 9, 13, 44, 0, 1));
        var id = (await this._repository.ListStorageAsync(this._accountId, 0)).Single().Id;

        Assert.That(await this._repository.TakeStorageItemAsync(this._otherAccountId, 0, id), Is.Null);
        var taken = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => this._repository.TakeStorageItemAsync(this._accountId, 0, id)));
        Assert.That(taken.Count(t => t is not null), Is.EqualTo(1));
        Assert.That(await this._repository.ListStorageAsync(this._accountId, 0), Is.Empty);

        await this._repository.RestoreStorageItemAsync(this._accountId, 0, taken.Single(t => t is not null)!);
        await this._repository.RestoreStorageItemAsync(this._accountId, 0, taken.Single(t => t is not null)!);
        Assert.That((await this._repository.ListStorageAsync(this._accountId, 0)).Select(i => i.Id), Is.EqualTo(new[] { id }));
    }
}
