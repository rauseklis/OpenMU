// <copyright file="JewelBankRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.JewelBank;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// Persists the Jewel Bank with plain SQL. Every mutation runs in one transaction which first claims the
/// (account, operation) pair, so a repeated request returns the stored outcome and applies nothing twice.
/// </summary>
public sealed class JewelBankRepository : IJewelBankRepository
{
    private const short DepositReason = 0;
    private const short WithdrawReason = 1;
    private const short PickupReason = 2;
    private const short DepositType = 0;
    private const short WithdrawType = 1;
    // Every unit is an item and an inventory holds 64 at most, so a request for more is not a request which can ever fit.
    private const int MaximumWithdrawItems = 256;

    private readonly Func<EntityDataContext> _contextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JewelBankRepository"/> class.
    /// </summary>
    /// <param name="contextFactory">Creates a context for each operation. The default creates a new context.</param>
    public JewelBankRepository(Func<EntityDataContext>? contextFactory = null)
    {
        this._contextFactory = contextFactory ?? (() => new EntityDataContext());
    }

    /// <inheritdoc />
    public async Task<JewelBankState> GetStateAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var context = this._contextFactory();
        return await ReadStateAsync(context, accountId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<JewelBankOperationResult> DepositFromInventoryAsync(Guid accountId, Guid characterId, Guid operationId, IReadOnlyDictionary<byte, long>? requested, long maximumPerKind, CancellationToken cancellationToken = default)
    {
        if (maximumPerKind <= 0 || (requested is not null && (requested.Keys.Any(code => JewelBankKinds.FindByCode(code) is null) || requested.Values.Any(amount => amount < 0))))
        {
            return JewelBankOperationResult.Refused(JewelBankResultCode.InvalidRequest);
        }

        await using var context = this._contextFactory();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (!await TryClaimOperationAsync(context, accountId, operationId, DepositType, now, cancellationToken))
        {
            return await ReplayAsync(context, accountId, operationId, cancellationToken);
        }

        var inventoryId = await FindInventoryAsync(context, characterId, cancellationToken);
        if (inventoryId is null)
        {
            return JewelBankOperationResult.Refused(JewelBankResultCode.InvalidRequest);
        }

        var definitions = await LoadDefinitionsAsync(context, cancellationToken);
        var items = await context.Set<Item>().AsNoTracking()
            .Include(i => i.RawItemOptions)
            .Where(i => i.ItemStorageId == inventoryId && i.ItemSlot >= InventoryConstants.EquippableSlotsCount)
            .OrderBy(i => i.ItemSlot)
            .ToListAsync(cancellationToken);

        // The deposit candidates of each kind, in slot order. Every item is one unit of its kind: a bundle is a unit of its own kind.
        var candidates = new Dictionary<byte, List<Guid>>();
        foreach (var item in items)
        {
            // Only items of durability 1 are single units. Anything else could be a stack, which would be counted wrongly, so it stays.
            if (item.DefinitionId is not { } definitionId || item.Durability != 1 || item.SocketCount > 0 || item.RawItemOptions.Count > 0)
            {
                continue;
            }

            if (Classify(definitions, definitionId, item.Level) is not { } kind)
            {
                continue;
            }

            if (!candidates.TryGetValue(kind.Code, out var ids))
            {
                ids = new List<Guid>();
                candidates[kind.Code] = ids;
            }

            ids.Add(item.Id);
        }

        var current = (await ReadStateAsync(context, accountId, cancellationToken)).Counts;
        var taken = new List<Guid>();
        var moved = new Dictionary<byte, long>();
        var sawCandidate = false;
        var hitCap = false;
        foreach (var (code, ids) in candidates)
        {
            long wanted;
            if (requested is null)
            {
                wanted = long.MaxValue;
            }
            else if (!requested.TryGetValue(code, out wanted) || wanted == 0)
            {
                continue;
            }

            sawCandidate = true;
            current.TryGetValue(code, out var stored);
            var room = Math.Max(0, maximumPerKind - stored);
            var count = (int)Math.Min(Math.Min(wanted, room), ids.Count);
            if (count == 0)
            {
                hitCap |= room == 0;
                continue;
            }

            taken.AddRange(ids.Take(count));
            moved[code] = count;
        }

        if (taken.Count == 0)
        {
            return JewelBankOperationResult.Refused(sawCandidate && hitCap ? JewelBankResultCode.CapReached : JewelBankResultCode.NothingToDeposit);
        }

        var deleted = await context.Set<Item>().Where(i => taken.Contains(i.Id) && i.ItemStorageId == inventoryId).ExecuteDeleteAsync(cancellationToken);
        if (deleted != taken.Count)
        {
            // An item vanished between the read and the delete (concurrent change): nothing may be credited for it.
            return JewelBankOperationResult.Refused(JewelBankResultCode.InvalidRequest);
        }

        foreach (var (kind, units) in moved)
        {
            if (await CreditAsync(context, accountId, characterId, operationId, kind, units, maximumPerKind, DepositReason, now, cancellationToken) is null)
            {
                return JewelBankOperationResult.Refused(JewelBankResultCode.CapReached);
            }
        }

        await FinishOperationAsync(context, accountId, operationId, JewelBankResultCode.Success, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new JewelBankOperationResult(JewelBankResultCode.Success, moved, await ReadStateAsync(context, accountId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<JewelBankOperationResult> WithdrawToInventoryAsync(Guid accountId, Guid characterId, Guid operationId, IReadOnlyDictionary<byte, long> units, CancellationToken cancellationToken = default)
    {
        var wanted = units.Where(p => p.Value != 0).ToList();
        if (wanted.Count == 0
            || wanted.Any(p => JewelBankKinds.FindByCode(p.Key) is null || p.Value < 0 || p.Value > MaximumWithdrawItems)
            || wanted.Sum(p => p.Value) > MaximumWithdrawItems)
        {
            return JewelBankOperationResult.Refused(JewelBankResultCode.InvalidRequest);
        }

        await using var context = this._contextFactory();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (!await TryClaimOperationAsync(context, accountId, operationId, WithdrawType, now, cancellationToken))
        {
            return await ReplayAsync(context, accountId, operationId, cancellationToken);
        }

        var inventoryId = await FindInventoryAsync(context, characterId, cancellationToken);
        if (inventoryId is null)
        {
            return JewelBankOperationResult.Refused(JewelBankResultCode.InvalidRequest);
        }

        var definitions = await LoadDefinitionsAsync(context, cancellationToken);
        var plan = new List<(Guid Definition, byte Level)>();
        foreach (var (code, amount) in wanted)
        {
            var kind = JewelBankKinds.FindByCode(code)!.Value;
            if (!definitions.DefinitionByKind.TryGetValue(code, out var definition))
            {
                return JewelBankOperationResult.Refused(JewelBankResultCode.NotConfigured);
            }

            plan.AddRange(Enumerable.Repeat((definition, kind.ItemLevel), (int)amount));
            if (!await DebitAsync(context, accountId, characterId, operationId, code, amount, now, cancellationToken))
            {
                return JewelBankOperationResult.Refused(JewelBankResultCode.InsufficientBalance);
            }
        }

        var sizes = new List<(int Width, int Height)>();
        foreach (var (definition, _) in plan)
        {
            sizes.Add(await AuctionHouseRepository.DefinitionSizeAsync(context, definition, cancellationToken));
        }

        var slots = await AuctionHouseRepository.PlaceAsync(context, inventoryId.Value, sizes, cancellationToken);
        if (slots is null)
        {
            return JewelBankOperationResult.Refused(JewelBankResultCode.InventoryFull);
        }

        for (var index = 0; index < plan.Count; index++)
        {
            context.Set<Item>().Add(new Item
            {
                Id = Guid.NewGuid(),
                ItemStorageId = inventoryId,
                ItemSlot = (byte)slots[index],
                DefinitionId = plan[index].Definition,
                Level = plan[index].Level,
                Durability = 1,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        await FinishOperationAsync(context, accountId, operationId, JewelBankResultCode.Success, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new JewelBankOperationResult(
            JewelBankResultCode.Success,
            wanted.ToDictionary(p => p.Key, p => p.Value),
            await ReadStateAsync(context, accountId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<long?> CreditPickupAsync(Guid accountId, Guid characterId, byte kindCode, long units, long maximumPerKind, CancellationToken cancellationToken = default)
    {
        if (JewelBankKinds.FindByCode(kindCode) is null || units <= 0 || maximumPerKind <= 0)
        {
            return null;
        }

        await using var context = this._contextFactory();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var credited = await CreditAsync(context, accountId, characterId, Guid.NewGuid(), kindCode, units, maximumPerKind, PickupReason, DateTimeOffset.UtcNow, cancellationToken);
        if (credited is null)
        {
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return credited;
    }

    /// <inheritdoc />
    public async Task SetAutoDepositAsync(Guid accountId, bool enabled, CancellationToken cancellationToken = default)
    {
        await using var context = this._contextFactory();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO data.\"JewelBankSettings\" (\"AccountId\", \"AutoDeposit\") VALUES ({accountId}, {enabled}) ON CONFLICT (\"AccountId\") DO UPDATE SET \"AutoDeposit\" = EXCLUDED.\"AutoDeposit\"",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        await using var context = this._contextFactory();
        var findings = new List<string>();
        var drift = await context.Database.SqlQuery<DriftRow>(
            $"SELECT b.\"AccountId\" AS \"AccountId\", b.\"Kind\"::int AS \"Kind\", b.\"Count\" AS \"Count\", COALESCE(SUM(m.\"Delta\"), 0)::bigint AS \"Sum\" FROM data.\"JewelBankBalance\" b LEFT JOIN data.\"JewelBankMovement\" m ON m.\"AccountId\" = b.\"AccountId\" AND m.\"Kind\" = b.\"Kind\" GROUP BY b.\"AccountId\", b.\"Kind\", b.\"Count\" HAVING b.\"Count\" <> COALESCE(SUM(m.\"Delta\"), 0) OR b.\"Count\" < 0")
            .ToListAsync(cancellationToken);
        findings.AddRange(drift.Select(d => $"Account {d.AccountId}, kind {d.Kind}: balance {d.Count} differs from its movements ({d.Sum})."));
        return findings;
    }

    private static JewelBankKind? Classify(Definitions definitions, Guid definitionId, int level)
        => definitions.IdToIdentity.TryGetValue(definitionId, out var identity) ? JewelBankKinds.Find(identity.Group, identity.Number, level) : null;

    private static async Task<JewelBankState> ReadStateAsync(EntityDataContext context, Guid accountId, CancellationToken cancellationToken)
    {
        var rows = await context.Database.SqlQuery<BalanceRow>(
                $"SELECT \"Kind\"::int AS \"Kind\", \"Count\" AS \"Count\" FROM data.\"JewelBankBalance\" WHERE \"AccountId\" = {accountId}")
            .ToListAsync(cancellationToken);
        var settings = await context.Database.SqlQuery<bool>(
                $"SELECT \"AutoDeposit\" AS \"Value\" FROM data.\"JewelBankSettings\" WHERE \"AccountId\" = {accountId}")
            .ToListAsync(cancellationToken);
        return new JewelBankState(rows.ToDictionary(r => (byte)r.Kind, r => r.Count), settings.Count > 0 && settings[0]);
    }

    private static async Task<Guid?> FindInventoryAsync(EntityDataContext context, Guid characterId, CancellationToken cancellationToken)
        => await context.Set<Character>().AsNoTracking()
            .Where(c => c.Id == characterId)
            .Select(c => c.InventoryId)
            .SingleOrDefaultAsync(cancellationToken);

    private static async Task<Definitions> LoadDefinitionsAsync(EntityDataContext context, CancellationToken cancellationToken)
    {
        var rows = await context.Database.SqlQuery<DefinitionRow>(
                $"SELECT d.\"Id\" AS \"Id\", d.\"Group\"::int AS \"Group\", d.\"Number\"::int AS \"Number\" FROM config.\"ItemDefinition\" d WHERE d.\"Group\" IN (12, 13, 14)")
            .ToListAsync(cancellationToken);
        var idToIdentity = new Dictionary<Guid, (byte Group, short Number)>();
        var definitionByKind = new Dictionary<byte, Guid>();
        foreach (var row in rows)
        {
            foreach (var kind in JewelBankKinds.All.Where(k => k.Group == row.Group && k.Number == row.Number))
            {
                idToIdentity[row.Id] = (kind.Group, kind.Number);
                definitionByKind[kind.Code] = row.Id;
            }
        }

        return new Definitions(idToIdentity, definitionByKind);
    }

    private static async Task<bool> TryClaimOperationAsync(EntityDataContext context, Guid accountId, Guid operationId, short type, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var inserted = await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO data.\"JewelBankOperation\" (\"Id\", \"AccountId\", \"OperationId\", \"Type\", \"Outcome\", \"CreatedAt\") VALUES ({Guid.NewGuid()}, {accountId}, {operationId}, {type}, -1, {now}) ON CONFLICT (\"AccountId\", \"OperationId\") DO NOTHING",
            cancellationToken);
        return inserted == 1;
    }

    private static async Task<JewelBankOperationResult> ReplayAsync(EntityDataContext context, Guid accountId, Guid operationId, CancellationToken cancellationToken)
    {
        var outcomes = await context.Database.SqlQuery<int>(
                $"SELECT \"Outcome\" AS \"Value\" FROM data.\"JewelBankOperation\" WHERE \"AccountId\" = {accountId} AND \"OperationId\" = {operationId}")
            .ToListAsync(cancellationToken);
        var result = outcomes.Count > 0 && outcomes[0] >= 0 ? (JewelBankResultCode)outcomes[0] : JewelBankResultCode.InvalidRequest;
        return new JewelBankOperationResult(result, new Dictionary<byte, long>(), await ReadStateAsync(context, accountId, cancellationToken));
    }

    private static async Task FinishOperationAsync(EntityDataContext context, Guid accountId, Guid operationId, JewelBankResultCode result, CancellationToken cancellationToken)
    {
        var outcome = (int)result;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE data.\"JewelBankOperation\" SET \"Outcome\" = {outcome} WHERE \"AccountId\" = {accountId} AND \"OperationId\" = {operationId}",
            cancellationToken);
    }

    private static async Task<long?> CreditAsync(EntityDataContext context, Guid accountId, Guid characterId, Guid operationId, byte kind, long units, long maximumPerKind, short reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (units > maximumPerKind)
        {
            return null;
        }

        var kindValue = (short)kind;
        var changed = await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO data.\"JewelBankBalance\" (\"AccountId\", \"Kind\", \"Count\", \"UpdatedAt\") VALUES ({accountId}, {kindValue}, {units}, {now}) ON CONFLICT (\"AccountId\", \"Kind\") DO UPDATE SET \"Count\" = data.\"JewelBankBalance\".\"Count\" + EXCLUDED.\"Count\", \"UpdatedAt\" = EXCLUDED.\"UpdatedAt\" WHERE data.\"JewelBankBalance\".\"Count\" + EXCLUDED.\"Count\" <= {maximumPerKind}",
            cancellationToken);
        if (changed == 0)
        {
            return null;
        }

        var balance = await ReadCountAsync(context, accountId, kindValue, cancellationToken);
        await AddMovementAsync(context, accountId, characterId, operationId, kind, units, balance, reason, now, cancellationToken);
        return balance;
    }

    private static async Task<bool> DebitAsync(EntityDataContext context, Guid accountId, Guid characterId, Guid operationId, byte kind, long units, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var kindValue = (short)kind;
        var changed = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE data.\"JewelBankBalance\" SET \"Count\" = \"Count\" - {units}, \"UpdatedAt\" = {now} WHERE \"AccountId\" = {accountId} AND \"Kind\" = {kindValue} AND \"Count\" >= {units}",
            cancellationToken);
        if (changed == 0)
        {
            return false;
        }

        var balance = await ReadCountAsync(context, accountId, kindValue, cancellationToken);
        await AddMovementAsync(context, accountId, characterId, operationId, kind, -units, balance, WithdrawReason, now, cancellationToken);
        return true;
    }

    private static async Task<long> ReadCountAsync(EntityDataContext context, Guid accountId, short kindValue, CancellationToken cancellationToken)
        => (await context.Database.SqlQuery<long>(
                $"SELECT \"Count\" AS \"Value\" FROM data.\"JewelBankBalance\" WHERE \"AccountId\" = {accountId} AND \"Kind\" = {kindValue}")
            .ToListAsync(cancellationToken)).Single();

    private static Task<int> AddMovementAsync(EntityDataContext context, Guid accountId, Guid characterId, Guid operationId, byte kind, long delta, long balance, short reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var kindValue = (short)kind;
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO data.\"JewelBankMovement\" (\"Id\", \"AccountId\", \"CharacterId\", \"OperationId\", \"Kind\", \"Delta\", \"Balance\", \"Reason\", \"CreatedAt\") VALUES ({Guid.NewGuid()}, {accountId}, {characterId}, {operationId}, {kindValue}, {delta}, {balance}, {reason}, {now})",
            cancellationToken);
    }

    private sealed record Definitions(
        Dictionary<Guid, (byte Group, short Number)> IdToIdentity,
        Dictionary<byte, Guid> DefinitionByKind);

    private sealed class BalanceRow
    {
        public int Kind { get; set; }

        public long Count { get; set; }
    }

    private sealed class DefinitionRow
    {
        public Guid Id { get; set; }

        public int Group { get; set; }

        public int Number { get; set; }
    }

    private sealed class DriftRow
    {
        public Guid AccountId { get; set; }

        public int Kind { get; set; }

        public long Count { get; set; }

        public long Sum { get; set; }
    }
}
