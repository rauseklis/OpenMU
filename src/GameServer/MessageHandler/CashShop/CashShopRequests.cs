// <copyright file="CashShopRequests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.CashShop;

using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.CashShop;

/// <summary>Authenticated Cash Shop requests, shared by the network handler and the integration tests.</summary>
internal static class CashShopRequests
{
    private const byte ErrorResult = 0xFF;

    /// <summary>Handles a request using only server-authenticated account/server identity.</summary>
    /// <param name="player">The authenticated player.</param>
    /// <param name="packet">The complete request.</param>
    /// <param name="repository">Persistent balances and storage, or null when unsupported.</param>
    /// <param name="catalog">The shop catalogue.</param>
    /// <param name="isInSafeZone">Overrides the safe zone check (for tests); by default the position of the player decides.</param>
    /// <returns>The packets to send back, in order; empty when no reply is appropriate.</returns>
    internal static async ValueTask<IReadOnlyList<byte[]>> HandleAsync(Player player, Memory<byte> packet, ICashShopRepository? repository, CashShopCatalog catalog, Func<Player, bool>? isInSafeZone = null)
    {
        if (!CashShopProtocol.IsValidRequest(packet.Span))
        {
            return [];
        }

        var data = packet.ToArray();
        var subCode = data[3];
        var inSafeZone = (isInSafeZone ?? (p => p.IsAtSafezone()))(player);
        var account = player.Account as Persistence.IIdentifiable;
        var authenticated = account is not null && player.SelectedCharacter is not null
            && player.PlayerState.CurrentState == PlayerState.EnteredWorld;
        if (subCode == 0x02)
        {
            if (packet.Span[4] == 1)
            {
                return [];
            }

            return [CashShopProtocol.Open(authenticated && repository is not null && inSafeZone)];
        }

        if (!authenticated || repository is null)
        {
            return [];
        }

        var accountId = account!.Id;
        var serverId = player.GetGameServerId();
        try
        {
            return subCode switch
            {
                0x01 => [CashShopProtocol.Balance(await repository.GetBalanceAsync(accountId, serverId).ConfigureAwait(false))],
                0x03 => [await BuyAsync(player, data, inSafeZone, repository, catalog, accountId, serverId).ConfigureAwait(false)],
                0x05 => await ListAsync(data, repository, accountId, serverId).ConfigureAwait(false),
                0x0A => [await DeleteAsync(data, repository, accountId, serverId).ConfigureAwait(false)],
                0x0B => [await UseAsync(player, data, inSafeZone, repository, accountId, serverId).ConfigureAwait(false)],
                _ => [CashShopProtocol.Refused(subCode)],
            };
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Cash Shop request {SubCode:X2} of account {AccountId} failed.", subCode, accountId);
            return subCode switch
            {
                0x03 => [CashShopProtocol.BuyResult(ErrorResult)],
                0x0A => [CashShopProtocol.DeleteResult(1)],
                0x0B => [CashShopProtocol.UseResult(ErrorResult)],
                _ => [],
            };
        }
    }

    private static async ValueTask<byte[]> BuyAsync(Player player, byte[] packet, bool inSafeZone, ICashShopRepository repository, CashShopCatalog catalog, Guid accountId, byte serverId)
    {
        var packageSeq = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4));
        var categorySeq = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(8));
        var priceSeq = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(12));
        if (!inSafeZone
            || !catalog.TryResolve(packageSeq, categorySeq, priceSeq, out var offer)
            || !CashShopItemDelivery.IsDeliverable(player, offer.ItemGroup, offer.ItemNumber, offer.ItemLevel))
        {
            return CashShopProtocol.BuyResult(4);
        }

        var item = new CashShopStorageItem(0, offer.PackageSeq, offer.ProductSeq, offer.PriceSeq, offer.ItemGroup, offer.ItemNumber, offer.ItemLevel, offer.Quantity, offer.RentalDurationSeconds);
        var result = await repository.PurchaseAsync(accountId, serverId, offer.Currency, offer.Price, item).ConfigureAwait(false);
        return CashShopProtocol.BuyResult(result switch
        {
            CashShopPurchaseResult.Success => 0,
            CashShopPurchaseResult.InsufficientFunds => 1,
            _ => 2,
        });
    }

    private static async ValueTask<IReadOnlyList<byte[]>> ListAsync(byte[] packet, ICashShopRepository repository, Guid accountId, byte serverId)
    {
        var page = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4));
        var storageType = (char)packet[8];

        // The gift box ('G') is not supported: gifting is not part of the shop.
        var items = storageType == 'G'
            ? []
            : await repository.ListStorageAsync(accountId, serverId).ConfigureAwait(false);
        return CashShopProtocol.StoragePage(items, page);
    }

    private static async ValueTask<byte[]> DeleteAsync(byte[] packet, ICashShopRepository repository, Guid accountId, byte serverId)
    {
        if (packet.Length < 12)
        {
            return CashShopProtocol.DeleteResult(1);
        }

        var storageItemId = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(8));
        var removed = await repository.TakeStorageItemAsync(accountId, serverId, storageItemId).ConfigureAwait(false);
        return CashShopProtocol.DeleteResult(removed is null ? (byte)1 : (byte)0);
    }

    private static async ValueTask<byte[]> UseAsync(Player player, byte[] packet, bool inSafeZone, ICashShopRepository repository, Guid accountId, byte serverId)
    {
        var storageItemId = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(8));
        if (!inSafeZone)
        {
            return CashShopProtocol.UseResult(22);
        }

        var stored = await repository.TakeStorageItemAsync(accountId, serverId, storageItemId).ConfigureAwait(false);
        if (stored is null)
        {
            return CashShopProtocol.UseResult(1);
        }

        bool delivered;
        try
        {
            delivered = await CashShopItemDelivery.TryDeliverAsync(player, stored).ConfigureAwait(false);
        }
        catch
        {
            await repository.RestoreStorageItemAsync(accountId, serverId, stored).ConfigureAwait(false);
            throw;
        }

        if (!delivered)
        {
            await repository.RestoreStorageItemAsync(accountId, serverId, stored).ConfigureAwait(false);
            return CashShopProtocol.UseResult(21);
        }

        return CashShopProtocol.UseResult(0);
    }
}
