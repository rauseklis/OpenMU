// <copyright file="AuctionCreateHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using Packets = MUnique.OpenMU.Network.Packets;
using Wire = MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Handles requests to list an inventory item for sale.
/// </summary>
[PlugIn]
[Guid("F84740D3-B5E7-4AF4-85FF-088BEDF049C2")]
[BelongsToGroup(AuctionHouseGroupHandlerPlugIn.GroupKey)]
internal sealed class AuctionCreateHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.AuctionCreateRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.AuctionCreateRequest.Length)
        {
            return;
        }

        var request = new Wire.AuctionCreateRequest(packet);
        var operationId = request.OperationId.ToArray();
        var service = AuctionHouseMutations.CreateService(player);
        var identity = AuctionHouseMutations.GetIdentity(player);
        if (identity is { } eligible && await AuctionHouseMutations.CheckEligibilityAsync(player, service, eligible.AccountId).ConfigureAwait(false) is { } refusal)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Create, refusal, 0, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        if (identity is { } limited && !AuctionHouseMutations.RateLimiter.TryAcquire(limited.AccountId, AuctionRequestCost.Create))
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Create, AuctionResultCode.RateLimited, 0, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        if (service is null || identity is not { } who)
        {
            await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Create, AuctionResultCode.ServiceUnavailable, 0, 0, 0, DateTimeOffset.UtcNow).ConfigureAwait(false);
            return;
        }

        var result = await player.RunPersistenceExclusiveAsync(async () =>
        {
            await player.SaveProgressAsync().ConfigureAwait(false);
            return await service.ListItemAsync(new AuctionListItemRequest(
                OperationId: new Guid(operationId),
                AccountId: who.AccountId,
                CharacterId: who.CharacterId,
                SellerName: who.Name,
                SellerIp: who.Ip,
                InventorySlot: request.InventorySlot,
                Currency: (byte)request.CurrencyMode,
                Category: 0,
                StartingPrice: request.StartingScalar,
                BuyoutPrice: request.BuyoutScalar == 0 ? null : (long?)request.BuyoutScalar,
                DurationHours: request.DurationHours,
                StartingFruits: request.CurrencyMode == Packets.AuctionCurrencyMode.Fruits
                    ? new FruitBasket(request.StartingStrength, request.StartingAgility, request.StartingVitality, request.StartingEnergy, request.StartingCommand)
                    : null,
                BuyoutFruits: request.CurrencyMode == Packets.AuctionCurrencyMode.Fruits && (long)request.BuyoutStrength + request.BuyoutAgility + request.BuyoutVitality + request.BuyoutEnergy + request.BuyoutCommand > 0
                    ? new FruitBasket(request.BuyoutStrength, request.BuyoutAgility, request.BuyoutVitality, request.BuyoutEnergy, request.BuyoutCommand)
                    : null)).ConfigureAwait(false);
        }).ConfigureAwait(false);

        await AuctionHouseMutations.SendOperationAsync(player, operationId, AuctionOperationType.Create, result.Result, (ulong)result.ListingNumber, 0, 0, result.ServerTime).ConfigureAwait(false);
        if (result.Result == AuctionResultCode.Success)
        {
            await AuctionHouseMutations.ReloadAfterChangeAsync(player).ConfigureAwait(false);
        }
    }
}
