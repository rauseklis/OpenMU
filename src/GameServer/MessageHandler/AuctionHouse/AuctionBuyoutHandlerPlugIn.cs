// <copyright file="AuctionBuyoutHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using Wire = MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Handles requests to buy a listing out at its buyout price.
/// </summary>
[PlugIn]
[Guid("F3719A5C-20E2-4CD1-B3C9-0B3AC048F639")]
[BelongsToGroup(AuctionHouseGroupHandlerPlugIn.GroupKey)]
internal sealed class AuctionBuyoutHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.AuctionBuyoutRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.AuctionBuyoutRequest.Length)
        {
            return;
        }

        var request = new Wire.AuctionBuyoutRequest(packet);
        var operationId = request.OperationId.ToArray();
        var service = AuctionHouseMutations.CreateService(player);
        var identity = AuctionHouseMutations.GetIdentity(player);
        if (identity is { } limited && !AuctionHouseMutations.RateLimiter.TryAcquire(limited.AccountId, AuctionRequestCost.Mutation))
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Buyout, AuctionResultCode.RateLimited, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        if (service is null || identity is not { } who)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Buyout, AuctionResultCode.ServiceUnavailable, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        var listingId = await service.ResolveListingIdAsync(unchecked((long)request.ListingId)).ConfigureAwait(false);
        if (listingId is not { } listing)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Buyout, AuctionResultCode.ListingNotFound, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        var result = await player.RunPersistenceExclusiveAsync(async () =>
        {
            await player.SaveProgressAsync().ConfigureAwait(false);
            return await service.BuyoutAsync(new AuctionBuyoutRequest(
                OperationId: new Guid(operationId),
                AccountId: who.AccountId,
                CharacterId: who.CharacterId,
                ListingId: listing,
                ExpectedVersion: (int)request.ExpectedVersion,
                BuyerIp: who.Ip)).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Buyout, result.Result, request.ListingId, 0, 0, result.ServerTime).ConfigureAwait(false);
        if (result.Result == AuctionResultCode.Success)
        {
            await AuctionHouseMutations.ReloadAfterChangeAsync(player).ConfigureAwait(false);
        }
    }
}
