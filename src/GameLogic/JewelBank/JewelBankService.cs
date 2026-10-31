// <copyright file="JewelBankService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.JewelBank;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.GuildWar;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// The Jewel Bank operations of a player: deposit, withdraw and the auto-deposit choice. It checks the gameplay state,
/// runs the repository operation while the player's own persistence is paused, and brings the live player up to date
/// afterwards, so the client never needs to reconnect.
/// </summary>
public sealed class JewelBankService
{
    private static readonly TimeSpan SettingCacheLifetime = TimeSpan.FromSeconds(30);
    private static readonly ConcurrentDictionary<Guid, (bool Enabled, DateTimeOffset LoadedAt)> AutoDepositCache = new();

    private readonly IJewelBankRepository _repository;
    private readonly IAuctionHouseRepository? _inventoryReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="JewelBankService"/> class.
    /// </summary>
    /// <param name="repository">The bank repository.</param>
    /// <param name="inventoryReader">Reads the committed inventory of a character, to refresh the live player.</param>
    public JewelBankService(IJewelBankRepository repository, IAuctionHouseRepository? inventoryReader)
    {
        this._repository = repository;
        this._inventoryReader = inventoryReader;
    }

    /// <summary>
    /// Gets the rate limiter shared by all requests of the server process.
    /// </summary>
    public static JewelBankRateLimiter RateLimiter { get; } = new(() => DateTimeOffset.UtcNow, JewelBankOptions.MinimumRequestInterval);

    /// <summary>
    /// Gets the limiter for the requests which only read the state (opening and refreshing the window). The window asks every
    /// second and a half, so this only stops a client which asks much faster.
    /// </summary>
    public static JewelBankRateLimiter OpenRateLimiter { get; } = new(() => DateTimeOffset.UtcNow, JewelBankOptions.MinimumOpenInterval);

    /// <summary>
    /// Creates the service for a player, or returns <c>null</c> when the game context has no Jewel Bank database.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The service, or <c>null</c>.</returns>
    public static JewelBankService? Create(Player player)
    {
        var provider = player.GameContext.PersistenceContextProvider;
        var repository = provider.CreateJewelBankRepository();
        return repository is null ? null : new JewelBankService(repository, provider.CreateAuctionHouseRepository());
    }

    /// <summary>
    /// Gets the (game server specific) account identifier and the character of the player, or <c>null</c> when no character is entered.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The identifiers, or <c>null</c>.</returns>
    public static (Guid AccountId, Guid CharacterId)? GetIdentity(Player player)
    {
        if (player.GetServerScopedAccountId() is not { } accountId || player.SelectedCharacter is not IIdentifiable character)
        {
            return null;
        }

        // The bank of each game server is kept under its own account identifier: nothing is shared between servers.
        return (accountId, character.Id);
    }

    /// <summary>
    /// Determines whether the player's state permits moving items to or from the bank by hand.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c> when the player stands in a safe zone and does nothing which conflicts.</returns>
    public static bool IsStateAllowed(Player player)
    {
        var state = new AuctionMutationState(
            HasEnteredWorld: player.PlayerState.CurrentState == PlayerState.EnteredWorld,
            IsAlive: player.IsAlive,
            IsInSafeZone: player.IsAtSafezone(),
            IsTeleporting: player.IsTeleporting,
            IsTrading: player.TradingPartner is not null,
            IsVending: player.ShopStorage?.StoreOpen ?? false,
            HasOpenedNpc: player.OpenedNpc is not null,
            IsDueling: player.DuelRoom is not null,
            IsInMiniGame: player.CurrentMiniGame is not null,
            IsInGuildWar: player.GuildWarContext?.State is GuildWarState.Requested or GuildWarState.Started);
        return AuctionMutationEligibility.IsAllowed(state);
    }

    /// <summary>
    /// Reads the stored counts and the auto-deposit choice of an account.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <returns>The state.</returns>
    public async Task<JewelBankState> GetStateAsync(Guid accountId)
    {
        var state = await this._repository.GetStateAsync(accountId).ConfigureAwait(false);
        PruneAutoDepositCache();
        AutoDepositCache[accountId] = (state.AutoDeposit, DateTimeOffset.UtcNow);
        return state;
    }

    /// <summary>
    /// Deposits bankable items of the player's inventory, up to the requested units per kind.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="operationId">The client-made operation identifier.</param>
    /// <param name="requested">The units to deposit by kind code, or <c>null</c> for everything the bank accepts.</param>
    /// <returns>The outcome.</returns>
    public async Task<JewelBankOperationResult> DepositAsync(Player player, Guid operationId, IReadOnlyDictionary<byte, long>? requested)
    {
        if (GetIdentity(player) is not { } who)
        {
            return JewelBankOperationResult.Refused(JewelBankResultCode.InvalidRequest);
        }

        var result = await player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!IsStateAllowed(player))
            {
                return JewelBankOperationResult.Refused(JewelBankResultCode.StateRestricted);
            }

            await player.SaveProgressAsync().ConfigureAwait(false);
            return await this._repository.DepositFromInventoryAsync(who.AccountId, who.CharacterId, operationId, requested, JewelBankOptions.MaximumPerKind).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await this.RefreshAfterChangeAsync(player, result).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Withdraws units of one or more kinds as items into the player's inventory.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="operationId">The client-made operation identifier.</param>
    /// <param name="units">The units to withdraw by kind code.</param>
    /// <returns>The outcome.</returns>
    public async Task<JewelBankOperationResult> WithdrawAsync(Player player, Guid operationId, IReadOnlyDictionary<byte, long> units)
    {
        if (GetIdentity(player) is not { } who)
        {
            return JewelBankOperationResult.Refused(JewelBankResultCode.InvalidRequest);
        }

        var result = await player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!IsStateAllowed(player))
            {
                return JewelBankOperationResult.Refused(JewelBankResultCode.StateRestricted);
            }

            await player.SaveProgressAsync().ConfigureAwait(false);
            return await this._repository.WithdrawToInventoryAsync(who.AccountId, who.CharacterId, operationId, units).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await this.RefreshAfterChangeAsync(player, result).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Plans how an amount of fruits is paid from the stored counts: the fruit type of which the account holds most is used
    /// first, so no type is emptied while another is plentiful.
    /// </summary>
    /// <param name="state">The stored counts.</param>
    /// <param name="amount">The number of fruits, of any type.</param>
    /// <returns>The units to take by kind code, or <c>null</c> when the account holds fewer fruits than that.</returns>
    public static IReadOnlyDictionary<byte, long>? PlanFruitSpend(JewelBankState state, long amount)
    {
        if (amount <= 0)
        {
            return null;
        }

        var plan = new Dictionary<byte, long>();
        var remaining = amount;
        foreach (var code in Enumerable.Range(JewelBankKinds.FirstFruitCode, JewelBankKinds.LastFruitCode - JewelBankKinds.FirstFruitCode + 1)
                     .Select(code => (byte)code)
                     .OrderByDescending(state.CountOf)
                     .ThenBy(code => code))
        {
            var take = Math.Min(remaining, state.CountOf(code));
            if (take <= 0)
            {
                continue;
            }

            plan[code] = take;
            remaining -= take;
            if (remaining == 0)
            {
                return plan;
            }
        }

        return null;
    }

    /// <summary>
    /// Counts the fruits (of any type) which an account holds in the bank.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <returns>The number of fruits.</returns>
    public async Task<long> GetFruitCountAsync(Guid accountId)
    {
        var state = await this.GetStateAsync(accountId).ConfigureAwait(false);
        return Enumerable.Range(JewelBankKinds.FirstFruitCode, JewelBankKinds.LastFruitCode - JewelBankKinds.FirstFruitCode + 1)
            .Sum(code => state.CountOf((byte)code));
    }

    /// <summary>
    /// Uses up fruits (of any type) from the bank of the player, for example as the price of a reset.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="amount">The number of fruits.</param>
    /// <returns>The outcome; <see cref="JewelBankResultCode.InsufficientBalance"/> when the bank holds fewer fruits.</returns>
    public async Task<JewelBankOperationResult> SpendFruitsAsync(Player player, Guid operationId, long amount)
    {
        if (GetIdentity(player) is not { } who)
        {
            return JewelBankOperationResult.Refused(JewelBankResultCode.InvalidRequest);
        }

        var plan = PlanFruitSpend(await this.GetStateAsync(who.AccountId).ConfigureAwait(false), amount);
        if (plan is null)
        {
            return JewelBankOperationResult.Refused(amount <= 0 ? JewelBankResultCode.InvalidRequest : JewelBankResultCode.InsufficientBalance);
        }

        return await this._repository.SpendAsync(who.AccountId, who.CharacterId, operationId, plan).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks the stored balances against their movements.
    /// </summary>
    /// <returns>A description of each broken invariant; empty when the data is consistent.</returns>
    public Task<IReadOnlyList<string>> ReconcileAsync() => this._repository.ReconcileAsync();

    /// <summary>
    /// Deletes request records which are older than the retention time.
    /// </summary>
    /// <param name="retention">How long request records are kept.</param>
    /// <param name="batchSize">The most records to delete in one call.</param>
    /// <returns>The number of deleted records.</returns>
    public Task<int> PurgeOperationsAsync(TimeSpan retention, int batchSize)
        => this._repository.PurgeOperationsAsync(DateTimeOffset.UtcNow - retention, batchSize);

    /// <summary>
    /// Stores the auto-deposit choice of an account.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="enabled">The choice.</param>
    /// <returns>A task which completes when the choice is stored.</returns>
    public async Task SetAutoDepositAsync(Guid accountId, bool enabled)
    {
        await this._repository.SetAutoDepositAsync(accountId, enabled).ConfigureAwait(false);
        AutoDepositCache[accountId] = (enabled, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Determines whether an account has switched auto-deposit on. The answer is cached for a short time, because every
    /// pickup of a helper asks for it.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <returns><c>true</c> when picked-up jewels and fruits should go to the bank.</returns>
    public async Task<bool> IsAutoDepositEnabledAsync(Guid accountId)
    {
        if (AutoDepositCache.TryGetValue(accountId, out var cached) && DateTimeOffset.UtcNow - cached.LoadedAt < SettingCacheLifetime)
        {
            return cached.Enabled;
        }

        return (await this.GetStateAsync(accountId).ConfigureAwait(false)).AutoDeposit;
    }

    private static void PruneAutoDepositCache()
    {
        if (AutoDepositCache.Count < 1024)
        {
            return;
        }

        // Expired entries are ignored by IsAutoDepositEnabledAsync anyway, so dropping them only frees memory.
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in AutoDepositCache)
        {
            if (now - entry.Value.LoadedAt >= SettingCacheLifetime)
            {
                AutoDepositCache.TryRemove(entry.Key, out _);
            }
        }
    }

    /// <summary>
    /// Credits a picked-up unit to the account.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="characterId">The character who picked the item up.</param>
    /// <param name="kindCode">The kind.</param>
    /// <param name="units">The units.</param>
    /// <returns>The new count of the kind, or <c>null</c> when the cap is reached.</returns>
    public Task<long?> CreditPickupAsync(Guid accountId, Guid characterId, byte kindCode, long units)
        => this._repository.CreditPickupAsync(accountId, characterId, kindCode, units, JewelBankOptions.MaximumPerKind);

    private async Task RefreshAfterChangeAsync(Player player, JewelBankOperationResult result)
    {
        if (!result.IsSuccess || this._inventoryReader is null || GetIdentity(player) is not { } who)
        {
            return;
        }

        try
        {
            await player.RunPersistenceExclusiveAsync(async () =>
            {
                var state = await this._inventoryReader.GetInventoryStateAsync(who.CharacterId).ConfigureAwait(false)
                            ?? throw new InvalidOperationException("The committed inventory state could not be read.");
                await PlayerInventoryRefresh.ApplyAsync(player, state).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "The Jewel Bank could not refresh the inventory of {Player} in place; disconnecting so the committed state is reloaded.", player);
            await player.DisconnectAsync().ConfigureAwait(false);
        }
    }
}
