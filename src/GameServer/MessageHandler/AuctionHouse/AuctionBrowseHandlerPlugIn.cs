// <copyright file="AuctionBrowseHandlerPlugIn.cs" company="MUnique">
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
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.PlugIns;
using Packets = MUnique.OpenMU.Network.Packets;
using Wire = MUnique.OpenMU.Network.Packets.ClientToServer;

/// <summary>
/// Handles browse requests. Each row carries the listing's item, serialized with the player's own item serializer.
/// </summary>
[PlugIn]
[Guid("C82CF3B7-A4B5-4E71-891F-020965BD7DC2")]
[BelongsToGroup(AuctionHouseGroupHandlerPlugIn.GroupKey)]
internal sealed class AuctionBrowseHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <summary>
    /// The category value of the request which browses every category.
    /// </summary>
    private const byte AllCategories = byte.MaxValue;

    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.AuctionBrowseRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.AuctionBrowseRequest.Length || player is not RemotePlayer remotePlayer || remotePlayer.Connection is not { } connection)
        {
            return;
        }

        var request = new Wire.AuctionBrowseRequest(packet);
        var now = DateTimeOffset.UtcNow;
        var service = AuctionHouseMutations.CreateService(player);
        var identity = AuctionHouseMutations.GetIdentity(player);
        var search = new AuctionListingSearch(
            Currency: (byte)request.CurrencyMode,
            Category: request.Category == AllCategories ? null : request.Category,
            Sort: (AuctionSearchSort)(int)request.Sort,
            Page: Math.Max(1, (int)request.Page));

        AuctionBrowseResult browse;
        if (identity is null)
        {
            browse = new AuctionBrowseResult(AuctionResultCode.NotAuthenticated, default, now);
        }
        else if (service is null)
        {
            browse = new AuctionBrowseResult(AuctionResultCode.ServiceUnavailable, default, now);
        }
        else
        {
            browse = await service.BrowseAsync(search).ConfigureAwait(false);
        }

        var rows = browse.Page.Rows;
        IReadOnlyDictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item> items = service is null || rows.Count == 0
            ? new Dictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item>()
            : await service.LoadItemsAsync(rows.Select(r => r.ItemId).Distinct().ToList()).ConfigureAwait(false);
        var serializer = remotePlayer.ItemSerializer;
        var pageSize = Math.Max(1, AuctionHouseMutations.Options.PageSize);
        var totalPages = Math.Max(1, (browse.Page.TotalCount + pageSize - 1) / pageSize);

        int WritePacket()
        {
            var length = AuctionBrowseResponseRef.GetRequiredSize(rows.Count);
            var response = new AuctionBrowseResponseRef(connection.Output.GetSpan(length)[..length]);
            response.RequestId = request.RequestId;
            response.Result = (Packets.AuctionResult)browse.Result;
            response.Page = (ushort)search.Page;
            response.TotalPages = (ushort)totalPages;
            response.TotalCount = (uint)browse.Page.TotalCount;
            response.ServerTime = (uint)browse.ServerTime.ToUnixTimeSeconds();
            response.ListingCount = (byte)rows.Count;
            var itemBuffer = new byte[64];
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var summary = response[index];
                summary.ListingId = (ulong)row.ListingNumber;
                summary.Version = (uint)row.Version;
                summary.Status = Packets.AuctionListingStatus.Active;
                summary.CurrencyMode = (Packets.AuctionCurrencyMode)row.Currency;
                summary.Category = (Packets.AuctionCategory)row.Category;
                summary.CurrentPriceScalar = (uint)(row.CurrentBid ?? row.StartingPrice);
                summary.CurrentPriceStrength = 0;
                summary.CurrentPriceAgility = 0;
                summary.CurrentPriceVitality = 0;
                summary.CurrentPriceEnergy = 0;
                summary.CurrentPriceCommand = 0;
                summary.BuyoutPriceScalar = (uint)(row.BuyoutPrice ?? 0);
                summary.BuyoutPriceStrength = 0;
                summary.BuyoutPriceAgility = 0;
                summary.BuyoutPriceVitality = 0;
                summary.BuyoutPriceEnergy = 0;
                summary.BuyoutPriceCommand = 0;
                summary.BidCount = (ushort)row.BidCount;
                summary.EndsAt = (uint)row.EndsAt.ToUnixTimeSeconds();
                var nameBytes = Encoding.UTF8.GetBytes(row.SellerName);
                var nameSpan = summary.SellerName;
                nameSpan.Clear();
                nameBytes.AsSpan(0, Math.Min(nameBytes.Length, nameSpan.Length)).CopyTo(nameSpan);
                var itemData = summary.ItemData;
                itemData.Clear();
                summary.ItemDataLength = 0;
                if (items.TryGetValue(row.ItemId, out var item))
                {
                    var itemLength = Math.Min(serializer.SerializeItem(itemBuffer, item), itemData.Length);
                    itemBuffer.AsSpan(0, itemLength).CopyTo(itemData);
                    summary.ItemDataLength = (byte)itemLength;
                }
            }

            return response.Header.Length;
        }

        await connection.SendAsync(WritePacket).ConfigureAwait(false);
    }
}
