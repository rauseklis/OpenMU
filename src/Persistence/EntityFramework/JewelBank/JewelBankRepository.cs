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
    private const int MaximumWithdrawUnits = 100_000;

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
    public async Task<JewelBankOperationResult> DepositFromInventoryAsync(Guid accountId, Guid characterId, Guid operationId, byte? kindCode, long maximumPerKind, CancellationToken cancellationToken = default)
    {
        if ((kindCode is { } code && JewelBankKinds.FindByCode(code) is null) || maximumPerKind <= 0)
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

        var current = (await ReadStateAsync(context, accountId, cancellationToken)).Counts.ToDictionary(p => p.Key, p => p.Value);
        var accepted = new List<(Guid ItemId, byte Kind, long Units)>();
        var added = new Dictionary<byte, long>();
        var sawCandidate = false;
        var hitCap = false;
        foreach (var item in items)
        {
            // Only items of durability 1 are single units. Anything else could be a stack, which would be counted wrongly, so it stays.
            if (item.DefinitionId is not { } definitionId || item.Durability != 1 || item.SocketCount > 0 || item.RawItemOptions.Count > 0)
            {
                continue;
            }

            var (kind, units) = Classify(definitions, definitionId, item.Level);
            if (kind is null || (kindCode is { } only && only != kind.Value.Code))
            {
                continue;
            }

            sawCandidate = true;
            current.TryGetValue(kind.Value.Code, out var stored);
            added.TryGetValue(kind.Value.Code, out var already);
            if (stored + already + units > maximumPerKind)
            {
                hitCap = true;
                continue;
            }

            accepted.Add((item.Id, kind.Value.Code, units));
            added[kind.Value.Code] = already + units;
        }

        if (accepted.Count == 0)
        {
            return JewelBankOperationResult.Refused(sawCandidate && hitCap ? JewelBankResultCode.CapReached : JewelBankResultCode.NothingToDeposit);
        }

        var ids = accepted.Select(a => a.ItemId).ToList();
        var deleted = await context.Set<Item>().Where(i => ids.Contains(i.Id) && i.ItemStorageId == inventoryId).ExecuteDeleteAsync(cancellationToken);
        if (deleted != ids.Count)
        {
            // An item vanished between the read and the delete (concurrent change): nothing may be credited for it.
            return JewelBankOperationResult.Refused(JewelBankResultCode.InvalidRequest);
        }

        foreach (var (kind, units) in added)
        {
            if (await CreditAsync(context, accountId, characterId, operationId, kind, units, maximumPerKind, DepositReason, now, cancellationToken) is null)
            {
                return JewelBankOperationResult.Refused(JewelBankResultCode.CapReached);
            }
        }

        await FinishOperationAsync(context, accountId, operationId, JewelBankResultCode.Success, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new JewelBankOperationResult(JewelBankResultCode.Success, added, await ReadStateAsync(context, accountId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<JewelBankOperationResult> WithdrawToInventoryAsync(Guid accountId, Guid characterId, Guid operationId, byte kindCode, long units, CancellationToken cancellationToken = default)
    {
        if (JewelBankKinds.FindByCode(kindCode) is not { } kind || units <= 0 || units > MaximumWithdrawUnits)
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
        if (!definitions.LooseByKind.TryGetValue(kind.Code, out var looseDefinition))
        {
            return JewelBankOperationResult.Refused(JewelBankResultCode.NotConfigured);
        }

        definitions.PackByKind.TryGetValue(kind.Code, out var packDefinition);
        var (packLevels, looseCount) = JewelBankKinds.Split(units, packDefinition != Guid.Empty);
        var plan = new List<(Guid Definition, byte Level)>();
        plan.AddRange(packLevels.Select(level => (packDefinition, level)));
        plan.AddRange(Enumerable.Repeat((looseDefinition, kind.FruitLevel ?? (byte)0), looseCount));

        var debited = await DebitAsync(context, accountId, characterId, operationId, kind.Code, units, now, cancellationToken);
        if (!debited)
        {
            return JewelBankOperationResult.Refused(JewelBankResultCode.InsufficientBalance);
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
            new Dictionary<byte, long> { [kind.Code] = units },
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

    private static (JewelBankKind? Kind, long Units) Classify(Definitions definitions, Guid definitionId, int level)
    {
        if (definitions.PackToKind.TryGetValue(definitionId, out var packKind))
        {
            var units = JewelBankKinds.GetPackUnits(level);
            return units > 0 && JewelBankKinds.FindByCode(packKind) is { } kind ? (kind, units) : (null, 0);
        }

        if (definitions.IdToIdentity.TryGetValue(definitionId, out var identity)
            && JewelBankKinds.Find(identity.Group, identity.Number, level) is { } looseKind)
        {
            return (looseKind, 1);
        }

        return (null, 0);
    }

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
        var looseByKind = new Dictionary<byte, Guid>();
        foreach (var row in rows)
        {
            foreach (var kind in JewelBankKinds.All.Where(k => k.Group == row.Group && k.Number == row.Number))
            {
                idToIdentity[row.Id] = (kind.Group, kind.Number);
                looseByKind[kind.Code] = row.Id;
            }
        }

        var mixes = await context.Database.SqlQuery<MixRow>(
                $"SELECT jm.\"SingleJewelId\" AS \"SingleId\", jm.\"MixedJewelId\" AS \"MixedId\" FROM config.\"JewelMix\" jm")
            .ToListAsync(cancellationToken);
        var packByKind = new Dictionary<byte, Guid>();
        var packToKind = new Dictionary<Guid, byte>();
        foreach (var mix in mixes)
        {
            var kind = JewelBankKinds.All.FirstOrDefault(k => !k.IsFruit && looseByKind.TryGetValue(k.Code, out var single) && single == mix.SingleId);
            if (kind.Code != 0)
            {
                packByKind[kind.Code] = mix.MixedId;
                packToKind[mix.MixedId] = kind.Code;
            }
        }

        return new Definitions(idToIdentity, looseByKind, packByKind, packToKind);
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
        Dictionary<byte, Guid> LooseByKind,
        Dictionary<byte, Guid> PackByKind,
        Dictionary<Guid, byte> PackToKind);

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

    private sealed class MixRow
    {
        public Guid SingleId { get; set; }

        public Guid MixedId { get; set; }
    }

    private sealed class DriftRow
    {
        public Guid AccountId { get; set; }

        public int Kind { get; set; }

        public long Count { get; set; }

        public long Sum { get; set; }
    }
}
