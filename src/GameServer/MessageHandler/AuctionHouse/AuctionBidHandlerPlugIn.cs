// <copyright file="AuctionBidHandlerPlugIn.cs" company="MUnique">
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
/// Handles requests to place a bid on a listing.
/// </summary>
[PlugIn]
[Guid("76E13E5D-F04F-4650-9666-A88839AF8088")]
[BelongsToGroup(AuctionHouseGroupHandlerPlugIn.GroupKey)]
internal sealed class AuctionBidHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.AuctionBidRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.AuctionBidRequest.Length)
        {
            return;
        }

        var request = new Wire.AuctionBidRequest(packet);
        var operationId = request.OperationId.ToArray();
        var service = AuctionHouseMutations.CreateService(player);
        var identity = AuctionHouseMutations.GetIdentity(player);
        if (identity is { } eligible && await AuctionHouseMutations.CheckEligibilityAsync(player, service, eligible.AccountId).ConfigureAwait(false) is { } refusal)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Bid, refusal, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        if (identity is { } limited && !AuctionHouseMutations.RateLimiter.TryAcquire(limited.AccountId, AuctionRequestCost.Mutation))
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Bid, AuctionResultCode.RateLimited, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        if (service is null || identity is not { } who)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Bid, AuctionResultCode.ServiceUnavailable, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        var listingId = await service.ResolveListingIdAsync(unchecked((long)request.ListingId)).ConfigureAwait(false);
        if (listingId is not { } listing)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Bid, AuctionResultCode.ListingNotFound, request.ListingId, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        var result = await player.RunPersistenceExclusiveAsync(async () =>
        {
            if (AuctionHouseMutations.CheckGameplayState(player) is { } stateRefusal)
            {
                return new AuctionMutationResult(stateRefusal, DateTimeOffset.UtcNow);
            }

            await player.SaveProgressAsync().ConfigureAwait(false);
            return await service.BidAsync(new AuctionBidRequest(
                OperationId: new Guid(operationId),
                AccountId: who.AccountId,
                CharacterId: who.CharacterId,
                ListingId: listing,
                ExpectedVersion: (int)request.ExpectedVersion,
                BidAmount: request.BidScalar,
                BidderIp: who.Ip,
                BidFruits: new FruitBasket(request.BidStrength, request.BidAgility, request.BidVitality, request.BidEnergy, request.BidCommand),
                BidBasket: BidBasketOf(request))).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Bid, result.Result, request.ListingId, 0, 0, result.ServerTime).ConfigureAwait(false);
        if (result.Result == AuctionResultCode.Success)
        {
            await AuctionHouseMutations.RefreshAfterChangeAsync(player, service).ConfigureAwait(false);
        }
    }

    private static PriceBasket? BidBasketOf(Wire.AuctionBidRequest request)
    {
        var basket = PriceBasketWire.Read(request.BidBasket);
        return basket.IsEmpty ? null : basket;
    }
}
