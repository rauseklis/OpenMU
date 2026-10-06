// <copyright file="AuctionDetailHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse;

using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;
using Packets = MUnique.OpenMU.Network.Packets;
using Wire = MUnique.OpenMU.Network.Packets.ClientToServer;

/// <summary>
/// Handles requests for the detail of one listing: its prices, its end time, its note, and its item.
/// </summary>
[PlugIn]
[Guid("6D2B7F4A-3C18-4E9B-8A52-B1F0C7D93E21")]
[BelongsToGroup(AuctionHouseGroupHandlerPlugIn.GroupKey)]
internal sealed class AuctionDetailHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.AuctionDetailRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.AuctionDetailRequest.Length || player is not RemotePlayer remotePlayer || remotePlayer.Connection is not { } connection)
        {
            return;
        }

        var request = new Wire.AuctionDetailRequest(packet);
        var service = AuctionHouseMutations.CreateService(player);
        var identity = AuctionHouseMutations.GetIdentity(player);
        var listingId = service is null || identity is null
            ? null
            : await service.ResolveListingIdAsync(unchecked((long)request.ListingId)).ConfigureAwait(false);

        AuctionDetailResult detail;
        if (identity is null)
        {
            detail = new AuctionDetailResult(AuctionResultCode.NotAuthenticated, null, DateTimeOffset.UtcNow);
        }
        else if (service is null)
        {
            detail = new AuctionDetailResult(AuctionResultCode.ServiceUnavailable, null, DateTimeOffset.UtcNow);
        }
        else if (listingId is not { } listing)
        {
            detail = new AuctionDetailResult(AuctionResultCode.ListingNotFound, null, DateTimeOffset.UtcNow);
        }
        else if (!AuctionHouseMutations.RateLimiter.TryAcquire(identity.Value.AccountId, AuctionRequestCost.Query))
        {
            detail = new AuctionDetailResult(AuctionResultCode.RateLimited, null, DateTimeOffset.UtcNow);
        }
        else
        {
            detail = await service.DetailAsync(listing).ConfigureAwait(false);
        }

        IReadOnlyDictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item> items = detail.Listing is { } view && service is not null
            ? await service.LoadItemsAsync(new[] { view.ItemId }).ConfigureAwait(false)
            : new Dictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item>();

        var itemBytes = new byte[64];
        var itemLength = 0;
        if (detail.Listing is { } shown && items.TryGetValue(shown.ItemId, out var item))
        {
            itemLength = Math.Min(remotePlayer.ItemSerializer.SerializeItem(itemBytes, item), 15);
        }

        var view2 = detail.Listing;
        var bid = view2?.CurrentBid ?? view2?.StartingPrice ?? 0;
        var noteBytes = Encoding.UTF8.GetBytes(view2?.Note ?? string.Empty);
        var noteLength = (byte)Math.Min(noteBytes.Length, 80);
        var sellerBytes = Encoding.UTF8.GetBytes(view2?.SellerName ?? string.Empty);
        var sellerName = sellerBytes.AsMemory(0, Math.Min(sellerBytes.Length, 10));

        await connection.SendAuctionDetailResponseAsync(
            request.RequestId,
            (Packets.AuctionResult)detail.Result,
            (uint)detail.ServerTime.ToUnixTimeSeconds(),
            request.ListingId,
            (uint)(view2?.Version ?? 0),
            (Packets.AuctionListingStatus)(view2?.Status ?? 0),
            (Packets.AuctionCurrencyMode)(view2?.Currency ?? 0),
            (Packets.AuctionCategory)(view2?.Category ?? 0),
            (uint)bid, 0, 0, 0, 0, 0,
            (uint)(view2?.BuyoutPrice ?? 0), 0, 0, 0, 0, 0,
            (ushort)(view2?.BidCount ?? 0),
            (uint)(view2?.OriginalEndsAt.ToUnixTimeSeconds() ?? 0),
            (uint)(view2?.EndsAt.ToUnixTimeSeconds() ?? 0),
            sellerName,
            Memory<byte>.Empty,
            noteLength,
            noteBytes.AsMemory(0, noteLength),
            (byte)itemLength,
            itemBytes.AsMemory(0, Math.Min(itemLength, 15))).ConfigureAwait(false);
    }
}
