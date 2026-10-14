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

    /// <summary>
    /// The currency value of the request which browses every currency.
    /// </summary>
    private const byte AllCurrencies = byte.MaxValue;

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
        var allCurrencies = (byte)request.CurrencyMode == AllCurrencies;
        var search = new AuctionListingSearch(
            Currency: allCurrencies ? null : (byte)request.CurrencyMode,
            Category: request.Category == AllCategories ? null : request.Category,
            SubCategory: AuctionTaxonomy.ToSubCategoryFilter(request.Category, request.SubCategory),
            Sort: (AuctionSearchSort)(int)request.Sort,
            Page: Math.Max(1, (int)request.Page),
            Name: DecodeName(request),
            LevelMinimum: request.LevelMinimum,
            LevelMaximum: request.LevelMaximum,
            MinimumScalar: allCurrencies ? 0 : request.MinimumScalar,
            MaximumScalar: allCurrencies ? 0 : request.MaximumScalar,
            MinimumFruits: allCurrencies ? null : new FruitBasket(request.MinimumStrength, request.MinimumAgility, request.MinimumVitality, request.MinimumEnergy, request.MinimumCommand),
            MaximumFruits: allCurrencies ? null : new FruitBasket(request.MaximumStrength, request.MaximumAgility, request.MaximumVitality, request.MaximumEnergy, request.MaximumCommand),
            MaxRemainingHours: request.MaxRemainingHours,
            ClassMask: request.ClassMask,
            OptionFlags: (AuctionItemOptionFlags)request.OptionFlags);

        AuctionBrowseResult browse;
        if (identity is null)
        {
            browse = new AuctionBrowseResult(AuctionResultCode.NotAuthenticated, default, now);
        }
        else if (service is null)
        {
            browse = new AuctionBrowseResult(AuctionResultCode.ServiceUnavailable, default, now);
        }
        else if (!AuctionHouseMutations.RateLimiter.TryAcquire(identity.Value.AccountId, AuctionRequestCost.Query))
        {
            browse = new AuctionBrowseResult(AuctionResultCode.RateLimited, default, now);
        }
        else
        {
            browse = await service.BrowseAsync(search).ConfigureAwait(false);
        }

        // Page.Rows is a reference-type property inside a record struct: every early-exit branch above
        // constructs its result with `default`, which leaves Rows null rather than empty. Coalesce here so a
        // disabled/unauthenticated/rate-limited/service-unavailable browse never null-refs below.
        var rows = browse.Page.Rows ?? Array.Empty<AuctionListingRow>();
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
                var isFruits = row.Currency == (byte)AuctionCurrencyMode.Fruits;
                var currentFruits = row.CurrentFruits ?? row.StartingFruits;
                var buyoutFruits = row.BuyoutFruits;
                summary.CurrentPriceScalar = isFruits ? 0 : (uint)(row.CurrentBid ?? row.StartingPrice);
                summary.CurrentPriceStrength = isFruits ? (uint)(currentFruits?.Strength ?? 0) : 0;
                summary.CurrentPriceAgility = isFruits ? (uint)(currentFruits?.Agility ?? 0) : 0;
                summary.CurrentPriceVitality = isFruits ? (uint)(currentFruits?.Vitality ?? 0) : 0;
                summary.CurrentPriceEnergy = isFruits ? (uint)(currentFruits?.Energy ?? 0) : 0;
                summary.CurrentPriceCommand = isFruits ? (uint)(currentFruits?.Command ?? 0) : 0;
                summary.BuyoutPriceScalar = isFruits ? 0 : (uint)(row.BuyoutPrice ?? 0);
                summary.BuyoutPriceStrength = isFruits ? (uint)(buyoutFruits?.Strength ?? 0) : 0;
                summary.BuyoutPriceAgility = isFruits ? (uint)(buyoutFruits?.Agility ?? 0) : 0;
                summary.BuyoutPriceVitality = isFruits ? (uint)(buyoutFruits?.Vitality ?? 0) : 0;
                summary.BuyoutPriceEnergy = isFruits ? (uint)(buyoutFruits?.Energy ?? 0) : 0;
                summary.BuyoutPriceCommand = isFruits ? (uint)(buyoutFruits?.Command ?? 0) : 0;
                PriceBasketWire.Write(summary.CurrentBasket, row.CurrentBidVector ?? row.PriceVector);
                PriceBasketWire.Write(summary.BuyoutBasket, row.BuyoutVector);
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
                    itemLength = ApplyListingOptionSnapshot(itemBuffer, itemLength, row.OptionFlags);
                    itemBuffer.AsSpan(0, itemLength).CopyTo(itemData);
                    summary.ItemDataLength = (byte)itemLength;
                }
            }

            return response.Header.Length;
        }

        await connection.SendAsync(WritePacket).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies the immutable option snapshot kept on an auction listing to the item bytes sent to the browse UI.
    /// A listed item must keep its visual rarity even if its option configuration is no longer loaded (for example
    /// after a seasonal configuration change). The extended item wire format is self-describing, so rebuilding its
    /// optional fields here preserves the original serializer output and fills only snapshot-backed flags.
    /// </summary>
    private static int ApplyListingOptionSnapshot(Span<byte> itemData, int itemLength, int optionFlags)
    {
        const byte HasSkill = 0x04;
        const byte HasExcellent = 0x08;
        const byte HasAncient = 0x10;
        const byte HasOption = 0x01;
        const byte HasHarmony = 0x20;
        const byte HasSockets = 0x80;
        const int HeaderLength = 5;

        if (itemLength < HeaderLength || itemLength > itemData.Length)
        {
            return itemLength;
        }

        var originalFlags = itemData[4];
        var snapshotFlags = (byte)optionFlags;
        var flags = (byte)(originalFlags | (snapshotFlags & (HasSkill | HasExcellent | HasAncient)));
        if (flags == originalFlags)
        {
            return itemLength;
        }

        Span<byte> rebuilt = stackalloc byte[15];
        itemData[..HeaderLength].CopyTo(rebuilt);
        rebuilt[4] = flags;
        var source = HeaderLength;
        var destination = HeaderLength;

        if (!CopyOptionalField(itemData, itemLength, rebuilt, originalFlags, flags, HasOption, 0, ref source, ref destination)
            || !CopyOptionalField(itemData, itemLength, rebuilt, originalFlags, flags, HasExcellent, 0x3F, ref source, ref destination)
            || !CopyOptionalField(itemData, itemLength, rebuilt, originalFlags, flags, HasAncient, 0x01, ref source, ref destination)
            || !CopyOptionalField(itemData, itemLength, rebuilt, originalFlags, flags, HasHarmony, 0, ref source, ref destination))
        {
            return itemLength;
        }

        if ((originalFlags & HasSockets) != 0)
        {
            if (source >= itemLength)
            {
                return itemLength;
            }

            var socketDescriptor = itemData[source++];
            if ((flags & HasSockets) != 0)
            {
                rebuilt[destination++] = socketDescriptor;
                var socketCount = socketDescriptor & 0x0F;
                if (source + socketCount > itemLength || destination + socketCount > rebuilt.Length)
                {
                    return itemLength;
                }

                itemData.Slice(source, socketCount).CopyTo(rebuilt[destination..]);
                destination += socketCount;
            }
        }

        rebuilt[..destination].CopyTo(itemData);
        return destination;
    }

    private static bool CopyOptionalField(Span<byte> sourceData, int sourceLength, Span<byte> destinationData,
        byte originalFlags, byte targetFlags, byte fieldFlag, byte defaultValue, ref int sourceOffset, ref int destinationOffset)
    {
        if ((originalFlags & fieldFlag) != 0)
        {
            if (sourceOffset >= sourceLength)
            {
                return false;
            }

            var value = sourceData[sourceOffset++];
            if ((targetFlags & fieldFlag) != 0)
            {
                destinationData[destinationOffset++] = value;
            }
        }
        else if ((targetFlags & fieldFlag) != 0)
        {
            destinationData[destinationOffset++] = defaultValue;
        }

        return true;
    }

    private static string? DecodeName(Wire.AuctionBrowseRequest request)
    {
        var length = Math.Min((int)request.NameLength, request.Name.Length);
        if (length == 0)
        {
            return null;
        }

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(request.Name[..length]);
        }
        catch (DecoderFallbackException)
        {
            return "\u0001";
        }
    }
}
