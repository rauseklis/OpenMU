// <copyright file="CashShopRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.CashShop;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.CashShop;

/// <summary>Reads committed wallets and credits all currencies atomically without account autosave caching.</summary>
public sealed class CashShopRepository : ICashShopRepository
{
    private readonly Func<EntityDataContext> _contextFactory;

    /// <summary>Initializes a repository with a fresh context for every request.</summary>
    /// <param name="contextFactory">An optional isolated context factory.</param>
    public CashShopRepository(Func<EntityDataContext>? contextFactory = null)
    {
        this._contextFactory = contextFactory ?? (() => new EntityDataContext());
    }

    /// <inheritdoc />
    public async Task<CashShopBalance> GetBalanceAsync(Guid accountId, byte serverId, CancellationToken cancellationToken = default)
    {
        await using var context = this._contextFactory();
        return await ReadAsync(context, accountId, serverId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CashShopBalance> CreditAsync(Guid accountId, byte serverId, Guid operationId, CashShopBalance credit, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credit);
        if (accountId == Guid.Empty || operationId == Guid.Empty || !credit.IsValid()
            || credit == new CashShopBalance() || string.IsNullOrWhiteSpace(reason) || reason.Length > 256)
        {
            throw new ArgumentException("Invalid Cash Shop administrative credit.");
        }

        await using var context = this._contextFactory();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var claimed = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO data."CashShopCredit"
                ("AccountId", "GameServerId", "OperationId", "WCoinC", "WCoinP", "GoblinPoints",
                 "ResultWCoinC", "ResultWCoinP", "ResultGoblinPoints", "Reason", "CreatedAt")
            VALUES ({accountId}, {(short)serverId}, {operationId}, {credit.WCoinC}, {credit.WCoinP},
                    {credit.GoblinPoints}, 0, 0, 0, {reason}, {DateTimeOffset.UtcNow})
            ON CONFLICT DO NOTHING
            """, cancellationToken).ConfigureAwait(false);
        if (claimed == 0)
        {
            var previous = await context.Database.SqlQuery<CreditRow>($"""
                SELECT "WCoinC", "WCoinP", "GoblinPoints", "ResultWCoinC", "ResultWCoinP", "ResultGoblinPoints", "Reason"
                FROM data."CashShopCredit"
                WHERE "AccountId"={accountId} AND "GameServerId"={(short)serverId} AND "OperationId"={operationId}
                """).SingleAsync(cancellationToken).ConfigureAwait(false);
            if (new CashShopBalance(previous.WCoinC, previous.WCoinP, previous.GoblinPoints) != credit || previous.Reason != reason)
            {
                throw new InvalidOperationException("The operation identifier belongs to a different credit.");
            }

            return new CashShopBalance(previous.ResultWCoinC, previous.ResultWCoinP, previous.ResultGoblinPoints);
        }

        var changed = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO data."CashShopBalance" AS b ("AccountId", "GameServerId", "WCoinC", "WCoinP", "GoblinPoints")
            VALUES ({accountId}, {(short)serverId}, {credit.WCoinC}, {credit.WCoinP}, {credit.GoblinPoints})
            ON CONFLICT ("AccountId", "GameServerId") DO UPDATE SET
                "WCoinC" = b."WCoinC" + EXCLUDED."WCoinC",
                "WCoinP" = b."WCoinP" + EXCLUDED."WCoinP",
                "GoblinPoints" = b."GoblinPoints" + EXCLUDED."GoblinPoints"
            WHERE b."WCoinC" <= {CashShopBalance.Maximum - credit.WCoinC}
              AND b."WCoinP" <= {CashShopBalance.Maximum - credit.WCoinP}
              AND b."GoblinPoints" <= {CashShopBalance.Maximum - credit.GoblinPoints}
            """, cancellationToken).ConfigureAwait(false);
        if (changed != 1)
        {
            throw new InvalidOperationException("The credit would exceed a Cash Shop currency limit.");
        }

        var result = await ReadAsync(context, accountId, serverId, cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE data."CashShopCredit"
            SET "ResultWCoinC"={result.WCoinC}, "ResultWCoinP"={result.WCoinP}, "ResultGoblinPoints"={result.GoblinPoints}
            WHERE "AccountId"={accountId} AND "GameServerId"={(short)serverId} AND "OperationId"={operationId}
            """, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>The most items one account can keep in its storage per game server.</summary>
    public const int MaximumStorageItems = 200;

    /// <inheritdoc />
    public async Task<CashShopPurchaseResult> PurchaseAsync(Guid accountId, byte serverId, CashShopCurrency currency, long price, CashShopStorageItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (accountId == Guid.Empty || price < 0 || price > CashShopBalance.Maximum || item.Quantity <= 0)
        {
            throw new ArgumentException("Invalid Cash Shop purchase.");
        }

        await using var context = this._contextFactory();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var debited = currency switch
        {
            CashShopCurrency.WCoinC => await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE data."CashShopBalance" SET "WCoinC" = "WCoinC" - {price}
                WHERE "AccountId"={accountId} AND "GameServerId"={(short)serverId} AND "WCoinC" >= {price}
                """, cancellationToken).ConfigureAwait(false),
            CashShopCurrency.WCoinP => await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE data."CashShopBalance" SET "WCoinP" = "WCoinP" - {price}
                WHERE "AccountId"={accountId} AND "GameServerId"={(short)serverId} AND "WCoinP" >= {price}
                """, cancellationToken).ConfigureAwait(false),
            CashShopCurrency.GoblinPoints => await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE data."CashShopBalance" SET "GoblinPoints" = "GoblinPoints" - {price}
                WHERE "AccountId"={accountId} AND "GameServerId"={(short)serverId} AND "GoblinPoints" >= {price}
                """, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(currency)),
        };

        if (debited != 1)
        {
            return CashShopPurchaseResult.InsufficientFunds;
        }

        var stored = await context.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value" FROM data."CashShopStorage"
            WHERE "AccountId"={accountId} AND "GameServerId"={(short)serverId}
            """).SingleAsync(cancellationToken).ConfigureAwait(false);
        if (stored >= MaximumStorageItems)
        {
            return CashShopPurchaseResult.StorageFull;
        }

        await InsertAsync(context, accountId, serverId, item, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return CashShopPurchaseResult.Success;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CashShopStorageItem>> ListStorageAsync(Guid accountId, byte serverId, CancellationToken cancellationToken = default)
    {
        await using var context = this._contextFactory();
        var rows = await context.Database.SqlQuery<StorageRow>($"""
            SELECT "Id", "PackageSeq", "ProductSeq", "PriceSeq", "ItemGroup", "ItemNumber", "ItemLevel", "Quantity", "RentalDurationSeconds"
            FROM data."CashShopStorage"
            WHERE "AccountId"={accountId} AND "GameServerId"={(short)serverId}
            ORDER BY "Id"
            """).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(r => r.ToItem()).ToList();
    }

    /// <inheritdoc />
    public async Task<CashShopStorageItem?> TakeStorageItemAsync(Guid accountId, byte serverId, long storageItemId, CancellationToken cancellationToken = default)
    {
        await using var context = this._contextFactory();
        var rows = await context.Database.SqlQuery<StorageRow>($"""
            DELETE FROM data."CashShopStorage"
            WHERE "Id"={storageItemId} AND "AccountId"={accountId} AND "GameServerId"={(short)serverId}
            RETURNING "Id", "PackageSeq", "ProductSeq", "PriceSeq", "ItemGroup", "ItemNumber", "ItemLevel", "Quantity", "RentalDurationSeconds"
            """).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0].ToItem();
    }

    /// <inheritdoc />
    public async Task RestoreStorageItemAsync(Guid accountId, byte serverId, CashShopStorageItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var context = this._contextFactory();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO data."CashShopStorage"
                ("Id", "AccountId", "GameServerId", "PackageSeq", "ProductSeq", "PriceSeq", "ItemGroup", "ItemNumber", "ItemLevel", "Quantity", "RentalDurationSeconds", "PurchasedAt")
            VALUES ({item.Id}, {accountId}, {(short)serverId}, {item.PackageSeq}, {item.ProductSeq}, {item.PriceSeq},
                    {(short)item.ItemGroup}, {item.ItemNumber}, {(short)item.ItemLevel}, {item.Quantity}, {item.RentalDurationSeconds}, {DateTimeOffset.UtcNow})
            ON CONFLICT DO NOTHING
            """, cancellationToken).ConfigureAwait(false);
    }

    private static Task<int> InsertAsync(EntityDataContext context, Guid accountId, byte serverId, CashShopStorageItem item, CancellationToken cancellationToken)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO data."CashShopStorage"
                ("AccountId", "GameServerId", "PackageSeq", "ProductSeq", "PriceSeq", "ItemGroup", "ItemNumber", "ItemLevel", "Quantity", "RentalDurationSeconds", "PurchasedAt")
            VALUES ({accountId}, {(short)serverId}, {item.PackageSeq}, {item.ProductSeq}, {item.PriceSeq},
                    {(short)item.ItemGroup}, {item.ItemNumber}, {(short)item.ItemLevel}, {item.Quantity}, {item.RentalDurationSeconds}, {DateTimeOffset.UtcNow})
            """, cancellationToken);

    private static async Task<CashShopBalance> ReadAsync(EntityDataContext context, Guid accountId, byte serverId, CancellationToken cancellationToken)
    {
        var row = await context.Database.SqlQuery<BalanceRow>($"""
            SELECT "WCoinC", "WCoinP", "GoblinPoints" FROM data."CashShopBalance"
            WHERE "AccountId"={accountId} AND "GameServerId"={(short)serverId}
            """).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? new CashShopBalance() : new CashShopBalance(row.WCoinC, row.WCoinP, row.GoblinPoints);
    }

    private sealed class BalanceRow
    {
        public long WCoinC { get; set; }

        public long WCoinP { get; set; }

        public long GoblinPoints { get; set; }
    }

    private sealed class StorageRow
    {
        public long Id { get; set; }

        public int PackageSeq { get; set; }

        public int ProductSeq { get; set; }

        public int PriceSeq { get; set; }

        public short ItemGroup { get; set; }

        public short ItemNumber { get; set; }

        public short ItemLevel { get; set; }

        public int Quantity { get; set; }

        public int? RentalDurationSeconds { get; set; }

        public CashShopStorageItem ToItem()
            => new(this.Id, this.PackageSeq, this.ProductSeq, this.PriceSeq, (byte)this.ItemGroup, this.ItemNumber, (byte)this.ItemLevel, this.Quantity, this.RentalDurationSeconds);
    }

    private sealed class CreditRow
    {
        public long WCoinC { get; set; }

        public long WCoinP { get; set; }

        public long GoblinPoints { get; set; }

        public long ResultWCoinC { get; set; }

        public long ResultWCoinP { get; set; }

        public long ResultGoblinPoints { get; set; }

        public string Reason { get; set; } = string.Empty;
    }
}
