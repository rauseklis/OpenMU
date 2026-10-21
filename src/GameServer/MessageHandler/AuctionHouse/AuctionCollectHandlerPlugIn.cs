// <copyright file="AuctionCollectHandlerPlugIn.cs" company="MUnique">
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
/// Handles requests to collect a pending Auction House collection.
/// </summary>
[PlugIn]
[Guid("D0F9F96C-F533-4A37-A6E4-6916C0208C0E")]
[BelongsToGroup(AuctionHouseGroupHandlerPlugIn.GroupKey)]
internal sealed class AuctionCollectHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.AuctionCollectRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.AuctionCollectRequest.Length)
        {
            return;
        }

        var request = new Wire.AuctionCollectRequest(packet);
        var operationId = request.OperationId.ToArray();
        var service = AuctionHouseMutations.CreateService(player);
        var identity = AuctionHouseMutations.GetIdentity(player);
        if (identity is { } eligible && await AuctionHouseMutations.CheckEligibilityAsync(player, service, eligible.AccountId).ConfigureAwait(false) is { } refusal)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Collect, refusal, 0, request.CollectionId, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        if (identity is { } limited && !AuctionHouseMutations.RateLimiter.TryAcquire(limited.AccountId, AuctionRequestCost.Create))
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Collect, AuctionResultCode.RateLimited, 0, request.CollectionId, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        if (service is null || identity is not { } who)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Collect, AuctionResultCode.ServiceUnavailable, 0, request.CollectionId, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        var collectionId = await service.ResolveCollectionIdAsync(unchecked((long)request.CollectionId)).ConfigureAwait(false);
        if (collectionId is not { } collection)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Collect, AuctionResultCode.CollectionNotFound, 0, request.CollectionId, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        var result = await player.RunPersistenceExclusiveAsync(async () =>
        {
            if (AuctionHouseMutations.CheckGameplayState(player) is { } stateRefusal)
            {
                return new AuctionCollectResult(stateRefusal, 0, null, 0, DateTimeOffset.UtcNow);
            }

            await player.SaveProgressAsync().ConfigureAwait(false);
            return await service.CollectAsync(new AuctionCollectRequest(
                OperationId: new Guid(operationId),
                AccountId: who.AccountId,
                CharacterId: who.CharacterId,
                CollectionId: collection,
                ExpectedVersion: (int)request.ExpectedVersion,
                RequestedScalar: request.RequestedScalar == 0 ? null : (long?)request.RequestedScalar,
                RequestedFruits: null)).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Collect, result.Result, 0, request.CollectionId, result.Amount, result.ServerTime).ConfigureAwait(false);
        if (result.Result == AuctionResultCode.Success)
        {
            await AuctionHouseMutations.RefreshAfterChangeAsync(player, service).ConfigureAwait(false);
        }
    }
}
