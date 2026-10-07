// <copyright file="AuctionCancelHandlerPlugIn.cs" company="MUnique">
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
/// Handles requests to cancel a listing which has no bid.
/// </summary>
[PlugIn]
[Guid("1F385BB4-D30E-487D-A31E-9AB9B262E95A")]
[BelongsToGroup(AuctionHouseGroupHandlerPlugIn.GroupKey)]
internal sealed class AuctionCancelHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.AuctionCancelRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.AuctionCancelRequest.Length)
        {
            return;
        }

        var request = new Wire.AuctionCancelRequest(packet);
        var operationId = request.OperationId.ToArray();
        var service = AuctionHouseMutations.CreateService(player);
        var identity = AuctionHouseMutations.GetIdentity(player);
        if (identity is { } eligible && await AuctionHouseMutations.CheckEligibilityAsync(player, service, eligible.AccountId).ConfigureAwait(false) is { } refusal)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Cancel, refusal, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        if (identity is { } limited && !AuctionHouseMutations.RateLimiter.TryAcquire(limited.AccountId, AuctionRequestCost.Create))
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Cancel, AuctionResultCode.RateLimited, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        if (service is null || identity is not { } who)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Cancel, AuctionResultCode.ServiceUnavailable, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        var listingId = await service.ResolveListingIdAsync(unchecked((long)request.ListingId)).ConfigureAwait(false);
        if (listingId is not { } listing)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Cancel, AuctionResultCode.ListingNotFound, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        var result = await player.RunPersistenceExclusiveAsync(async () =>
        {
            if (AuctionHouseMutations.CheckGameplayState(player) is { } stateRefusal)
            {
                return new AuctionMutationResult(stateRefusal, DateTimeOffset.UtcNow);
            }

            await player.SaveProgressAsync().ConfigureAwait(false);
            return await service.CancelAsync(new AuctionCancelRequest(new Guid(operationId), who.AccountId, listing, (int)request.ExpectedVersion)).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Cancel, result.Result, request.ListingId, 0, 0, result.ServerTime).ConfigureAwait(false);
    }
}
