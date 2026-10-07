// <copyright file="AuctionMailboxHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse;

using System.Runtime.InteropServices;
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
/// Handles mailbox requests: the account's collections, with their original and remaining amounts and their item.
/// </summary>
[PlugIn]
[Guid("3F8C5A21-9E47-4B6D-A1C2-7D0E94B8F632")]
[BelongsToGroup(AuctionHouseGroupHandlerPlugIn.GroupKey)]
internal sealed class AuctionMailboxHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <summary>
    /// The kind filter of the request which shows every kind.
    /// </summary>
    private const byte AllKinds = byte.MaxValue;

    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.AuctionMailboxRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.AuctionMailboxRequest.Length || player is not RemotePlayer remotePlayer || remotePlayer.Connection is not { } connection)
        {
            return;
        }

        var request = new Wire.AuctionMailboxRequest(packet);
        var now = DateTimeOffset.UtcNow;
        var service = AuctionHouseMutations.CreateService(player);
        var identity = AuctionHouseMutations.GetIdentity(player);
        var kind = request.KindFilter == AllKinds ? (byte?)null : request.KindFilter;
        var page = Math.Max(1, (int)request.Page);

        AuctionMailboxResult mailbox;
        if (identity is null)
        {
            mailbox = new AuctionMailboxResult(AuctionResultCode.NotAuthenticated, default, now);
        }
        else if (service is null)
        {
            mailbox = new AuctionMailboxResult(AuctionResultCode.ServiceUnavailable, default, now);
        }
        else if (!AuctionHouseMutations.RateLimiter.TryAcquire(identity.Value.AccountId, AuctionRequestCost.Query))
        {
            mailbox = new AuctionMailboxResult(AuctionResultCode.RateLimited, default, now);
        }
        else
        {
            mailbox = await service.MailboxAsync(identity.Value.AccountId, page, kind).ConfigureAwait(false);
        }

        // Page.Rows is a reference-type property inside a record struct: every early-exit branch above
        // constructs its result with `default`, which leaves Rows null rather than empty. Coalesce here so a
        // disabled/unauthenticated/rate-limited/service-unavailable mailbox request never null-refs below.
        var rows = mailbox.Page.Rows ?? Array.Empty<AuctionCollectionRow>();
        var itemIds = rows.Where(r => r.ItemId is not null).Select(r => r.ItemId!.Value).Distinct().ToList();
        IReadOnlyDictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item> items = service is null || itemIds.Count == 0
            ? new Dictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item>()
            : await service.LoadItemsAsync(itemIds).ConfigureAwait(false);
        var serializer = remotePlayer.ItemSerializer;
        var pageSize = Math.Max(1, AuctionHouseMutations.Options.PageSize);
        var totalPages = Math.Max(1, (mailbox.Page.TotalCount + pageSize - 1) / pageSize);

        int WritePacket()
        {
            var length = AuctionMailboxResponseRef.GetRequiredSize(rows.Count);
            var response = new AuctionMailboxResponseRef(connection.Output.GetSpan(length)[..length]);
            response.RequestId = request.RequestId;
            response.Result = (Packets.AuctionResult)mailbox.Result;
            response.Page = (ushort)page;
            response.TotalPages = (ushort)totalPages;
            response.TotalCount = (uint)mailbox.Page.TotalCount;
            response.ServerTime = (uint)mailbox.ServerTime.ToUnixTimeSeconds();
            response.EntryCount = (byte)rows.Count;
            var itemBuffer = new byte[64];
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var entry = response[index];
                entry.CollectionId = (ulong)row.CollectionNumber;
                entry.CollectionKind = (Packets.AuctionCollectionKind)row.Kind;
                entry.CollectionStatus = (Packets.AuctionCollectionStatus)row.Status;
                entry.CurrencyMode = (Packets.AuctionCurrencyMode)row.Currency;
                entry.OriginalScalar = (uint)(row.OriginalScalar ?? 0);
                entry.OriginalStrength = (uint)(row.OriginalFruits?.Strength ?? 0);
                entry.OriginalAgility = (uint)(row.OriginalFruits?.Agility ?? 0);
                entry.OriginalVitality = (uint)(row.OriginalFruits?.Vitality ?? 0);
                entry.OriginalEnergy = (uint)(row.OriginalFruits?.Energy ?? 0);
                entry.OriginalCommand = (uint)(row.OriginalFruits?.Command ?? 0);
                entry.RemainingScalar = (uint)(row.RemainingScalar ?? 0);
                entry.RemainingStrength = (uint)(row.RemainingFruits?.Strength ?? 0);
                entry.RemainingAgility = (uint)(row.RemainingFruits?.Agility ?? 0);
                entry.RemainingVitality = (uint)(row.RemainingFruits?.Vitality ?? 0);
                entry.RemainingEnergy = (uint)(row.RemainingFruits?.Energy ?? 0);
                entry.RemainingCommand = (uint)(row.RemainingFruits?.Command ?? 0);
                entry.SourceListingId = (ulong)row.SourceListingNumber;
                entry.Version = (uint)row.Version;
                var itemData = entry.ItemData;
                itemData.Clear();
                entry.ItemDataLength = 0;
                MUnique.OpenMU.DataModel.Entities.Item? item = null;
                var hasItem = row.ItemId is { } itemId && items.TryGetValue(itemId, out item);
                entry.HasItem = hasItem;
                if (hasItem)
                {
                    var itemLength = Math.Min(serializer.SerializeItem(itemBuffer, item!), itemData.Length);
                    itemBuffer.AsSpan(0, itemLength).CopyTo(itemData);
                    entry.ItemDataLength = (byte)itemLength;
                }
            }

            return response.Header.Length;
        }

        await connection.SendAsync(WritePacket).ConfigureAwait(false);
    }
}
