// <copyright file="AuctionMyListingsHandlerPlugIn.cs" company="MUnique">
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
/// Handles requests for the character's own listings, with the status of each listing.
/// </summary>
[PlugIn]
[Guid("A4E19C27-58B3-4F0D-9E61-3D8B2C75F04A")]
[BelongsToGroup(AuctionHouseGroupHandlerPlugIn.GroupKey)]
internal sealed class AuctionMyListingsHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <summary>
    /// The status filter of the request which shows every status.
    /// </summary>
    private const byte AllStatuses = byte.MaxValue;

    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.AuctionMyListingsRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.AuctionMyListingsRequest.Length || player is not RemotePlayer remotePlayer || remotePlayer.Connection is not { } connection)
        {
            return;
        }

        var request = new Wire.AuctionMyListingsRequest(packet);
        var now = DateTimeOffset.UtcNow;
        var service = AuctionHouseMutations.CreateService(player);
        var identity = AuctionHouseMutations.GetIdentity(player);
        var status = request.StatusFilter == AllStatuses ? (byte?)null : request.StatusFilter;
        var page = Math.Max(1, (int)request.Page);

        AuctionBrowseResult listed;
        if (identity is null)
        {
            listed = new AuctionBrowseResult(AuctionResultCode.NotAuthenticated, default, now);
        }
        else if (service is null)
        {
            listed = new AuctionBrowseResult(AuctionResultCode.ServiceUnavailable, default, now);
        }
        else if (!AuctionHouseMutations.RateLimiter.TryAcquire(identity.Value.AccountId, AuctionRequestCost.Query))
        {
            listed = new AuctionBrowseResult(AuctionResultCode.RateLimited, default, now);
        }
        else
        {
            listed = await service.MyListingsAsync(identity.Value.AccountId, page, status).ConfigureAwait(false);
        }

        var rows = listed.Page.Rows;
        IReadOnlyDictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item> items = service is null || rows.Count == 0
            ? new Dictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item>()
            : await service.LoadItemsAsync(rows.Select(r => r.ItemId).Distinct().ToList()).ConfigureAwait(false);
        var serializer = remotePlayer.ItemSerializer;
        var pageSize = Math.Max(1, AuctionHouseMutations.Options.PageSize);
        var totalPages = Math.Max(1, (listed.Page.TotalCount + pageSize - 1) / pageSize);

        int WritePacket()
        {
            var length = AuctionMyListingsResponseRef.GetRequiredSize(rows.Count);
            var response = new AuctionMyListingsResponseRef(connection.Output.GetSpan(length)[..length]);
            response.RequestId = request.RequestId;
            response.Result = (Packets.AuctionResult)listed.Result;
            response.Page = (ushort)page;
            response.TotalPages = (ushort)totalPages;
            response.TotalCount = (uint)listed.Page.TotalCount;
            response.ServerTime = (uint)listed.ServerTime.ToUnixTimeSeconds();
            response.ListingCount = (byte)rows.Count;
            var itemBuffer = new byte[64];
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var summary = response[index];
                summary.ListingId = (ulong)row.ListingNumber;
                summary.Version = (uint)row.Version;
                summary.Status = (Packets.AuctionListingStatus)row.Status;
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
