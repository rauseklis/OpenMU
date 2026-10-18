// <copyright file="AuctionHouseMutations.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.GuildWar;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// The shared steps of the Auction House request handlers: the identity of the requesting character, the service, the
/// operation response, and the reload of a player whose inventory or Zen changed.
/// </summary>
/// <remarks>
/// A successful operation which changes the player's inventory or Zen is followed by an in-place refresh of the live
/// player, see <see cref="PlayerInventoryRefresh"/>.
/// </remarks>
internal static class AuctionHouseMutations
{
    /// <summary>
    /// Gets the options in effect, from the configuration which the expiry worker publishes.
    /// </summary>
    public static AuctionHouseOptions Options => AuctionHouseConfigurationSource.Current.ToOptions();

    /// <summary>
    /// Gets the per-account request budgets. Every Auction House request takes a token before it reaches the service.
    /// </summary>
    public static AuctionRateLimiter RateLimiter { get; } = new(() => DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates the service, or returns <c>null</c> when the game context has no Auction House database.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <returns>The service, or <c>null</c>.</returns>
    public static AuctionHouseService? CreateService(Player player)
    {
        var repository = player.GameContext.PersistenceContextProvider.CreateAuctionHouseRepository();
        return repository is null ? null : new AuctionHouseService(repository, Options);
    }

    /// <summary>
    /// Checks the requirements of a mutation: the account is not restricted, the character is high enough, and the account is old enough.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="service">The service, or <c>null</c> when the Auction House database is unavailable.</param>
    /// <param name="accountId">The requesting account.</param>
    /// <returns>The refusal, or <c>null</c> when the requester is eligible.</returns>
    public static async Task<AuctionResultCode?> CheckEligibilityAsync(Player player, AuctionHouseService? service, Guid accountId)
    {
        if (service is null)
        {
            return null;
        }

        if (CheckGameplayState(player) is { } stateRefusal)
        {
            return stateRefusal;
        }

        var now = DateTimeOffset.UtcNow;
        var restricted = await service.IsRestrictedAsync(accountId, now).ConfigureAwait(false);
        var configuration = AuctionHouseConfigurationSource.Current;
        return AuctionEligibility.Evaluate(
            (int)(player.Attributes?[Stats.Level] ?? 0),
            player.Account?.RegistrationDate ?? now.UtcDateTime,
            now.UtcDateTime,
            configuration.MinimumCharacterLevel,
            configuration.MinimumAccountAgeHours,
            restricted);
    }

    /// <summary>
    /// Checks whether the player's current gameplay state permits an Auction House mutation.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <returns>The state refusal, or <c>null</c> when the mutation may proceed.</returns>
    public static AuctionResultCode? CheckGameplayState(Player player)
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
        return AuctionMutationEligibility.IsAllowed(state) ? null : AuctionResultCode.StateRestricted;
    }

    /// <summary>
    /// Gets the identity of the requesting character, or <c>null</c> when no character is entered.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <returns>The account, character, name, and address; or <c>null</c>.</returns>
    public static (Guid AccountId, Guid CharacterId, string Name, string? Ip)? GetIdentity(Player player)
    {
        if (player.Account is not MUnique.OpenMU.Persistence.IIdentifiable account || player.SelectedCharacter is not { } character)
        {
            return null;
        }

        return (account.Id, character.Id, character.Name, (player as IHasIpAddress)?.IpAddress);
    }

    /// <summary>
    /// Sends the operation response to the player.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="operationId">The operation identifier of the request.</param>
    /// <param name="operationType">The kind of operation.</param>
    /// <param name="result">The outcome.</param>
    /// <param name="listingNumber">The listing number on the wire, or zero.</param>
    /// <param name="collectionNumber">The collection number on the wire, or zero.</param>
    /// <param name="claimedScalar">The amount collected, or zero.</param>
    /// <param name="serverTime">The server time, in UTC.</param>
    /// <returns>A task which completes when the response is sent.</returns>
    public static async ValueTask SendOperationAsync(Player player, byte[] operationId, AuctionOperationType operationType, AuctionResultCode result, ulong listingNumber, ulong collectionNumber, long claimedScalar, DateTimeOffset serverTime, long feeScalar = 0, FruitBasket? feeFruits = null, byte currency = 0)
    {
        if (player is not RemotePlayer { Connection: { } connection })
        {
            return;
        }

        await connection.SendAuctionOperationResponseAsync(
            operationId,
            operationType,
            (AuctionResult)result,
            listingNumber,
            collectionNumber,
            0,
            (MUnique.OpenMU.Network.Packets.AuctionCurrencyMode)currency,
            (uint)feeScalar, (uint)(feeFruits?.Strength ?? 0), (uint)(feeFruits?.Agility ?? 0), (uint)(feeFruits?.Vitality ?? 0), (uint)(feeFruits?.Energy ?? 0), (uint)(feeFruits?.Command ?? 0),
            0, 0, 0, 0, 0, 0,
            (uint)claimedScalar, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            (uint)serverTime.ToUnixTimeSeconds()).ConfigureAwait(false);
    }

    /// <summary>
    /// Brings the live player up to date after a committed change to the inventory or Zen: the items which left or arrived and the
    /// Zen are applied to the in-memory player, and the client is sent the new inventory. If that fails, the player is disconnected,
    /// so the committed state is reloaded when the player comes back.
    /// </summary>
    /// <param name="player">The player whose state changed.</param>
    /// <param name="service">The service which committed the change.</param>
    /// <returns>A task which completes when the player is up to date or disconnected.</returns>
    public static async ValueTask RefreshAfterChangeAsync(Player player, AuctionHouseService service)
    {
        try
        {
            await player.RunPersistenceExclusiveAsync(async () =>
            {
                var state = GetIdentity(player) is { } who ? await service.GetInventoryStateAsync(who.CharacterId).ConfigureAwait(false) : null;
                if (state is null)
                {
                    throw new InvalidOperationException("The committed inventory state could not be read.");
                }

                await PlayerInventoryRefresh.ApplyAsync(player, state).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "The Auction House could not refresh the inventory of {Player} in place; disconnecting so the committed state is reloaded.", player);
            await player.DisconnectAsync().ConfigureAwait(false);
        }
    }
}
