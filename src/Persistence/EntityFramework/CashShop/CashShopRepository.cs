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
