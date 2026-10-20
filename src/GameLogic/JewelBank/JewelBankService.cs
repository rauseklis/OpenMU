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
    /// Gets the account and character of the player, or <c>null</c> when no character is entered.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The identifiers, or <c>null</c>.</returns>
    public static (Guid AccountId, Guid CharacterId)? GetIdentity(Player player)
    {
        if (player.Account is not IIdentifiable account || player.SelectedCharacter is not IIdentifiable character)
        {
            return null;
        }

        return (account.Id, character.Id);
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
        AutoDepositCache[accountId] = (state.AutoDeposit, DateTimeOffset.UtcNow);
        return state;
    }

    /// <summary>
    /// Deposits the bankable items of the player's inventory.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="operationId">The client-made operation identifier.</param>
    /// <param name="kindCode">The only kind to deposit, or <c>null</c> for every kind.</param>
    /// <returns>The outcome.</returns>
    public async Task<JewelBankOperationResult> DepositAsync(Player player, Guid operationId, byte? kindCode)
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
            return await this._repository.DepositFromInventoryAsync(who.AccountId, who.CharacterId, operationId, kindCode, JewelBankOptions.MaximumPerKind).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await this.RefreshAfterChangeAsync(player, result).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Withdraws units of a kind as items into the player's inventory.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="operationId">The client-made operation identifier.</param>
    /// <param name="kindCode">The kind.</param>
    /// <param name="units">The amount of units.</param>
    /// <returns>The outcome.</returns>
    public async Task<JewelBankOperationResult> WithdrawAsync(Player player, Guid operationId, byte kindCode, long units)
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
            return await this._repository.WithdrawToInventoryAsync(who.AccountId, who.CharacterId, operationId, kindCode, units).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await this.RefreshAfterChangeAsync(player, result).ConfigureAwait(false);
        return result;
    }

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
