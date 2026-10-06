// <copyright file="AuctionOpenHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;
using Wire = MUnique.OpenMU.Network.Packets.ClientToServer;

/// <summary>
/// Handles requests to open the Auction House window. The response reports the fees, the durations, the Zen rules, the
/// spendable Zen and the pending Mailbox count.
/// </summary>
[PlugIn]
[Guid("58895FA4-57D0-441F-9702-FEEE0898C754")]
[BelongsToGroup(AuctionHouseGroupHandlerPlugIn.GroupKey)]
internal sealed class AuctionOpenHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private const byte CurrencyCount = 9;

    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.AuctionOpenRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.AuctionOpenRequest.Length || player is not RemotePlayer { Connection: { } connection })
        {
            return;
        }

        var request = new Wire.AuctionOpenRequest(packet);
        var now = DateTimeOffset.UtcNow;
        var options = AuctionHouseMutations.Options;
        var service = AuctionHouseMutations.CreateService(player);
        var identity = AuctionHouseMutations.GetIdentity(player);
        var open = identity is not { } who
            ? new AuctionOpenResult(AuctionResultCode.NotAuthenticated, 0, now, options.PageSize)
            : service is null
                ? new AuctionOpenResult(AuctionResultCode.ServiceUnavailable, 0, now, options.PageSize)
                : await service.OpenAsync(who.AccountId).ConfigureAwait(false);
        var money = (uint)Math.Max(0, player.SelectedCharacter?.Inventory?.Money ?? 0);

        int WritePacket()
        {
            var length = AuctionOpenResponseRef.GetRequiredSize(CurrencyCount);
            var response = new AuctionOpenResponseRef(connection.Output.GetSpan(length)[..length]);
            response.RequestId = request.RequestId;
            response.Result = (AuctionResult)open.Result;
            response.ServerTime = (uint)open.ServerTime.ToUnixTimeSeconds();
            response.ConfigurationVersion = 1;
            response.ListingFeeBasisPoints = (ushort)options.ListingFeeBasisPoints;
            response.SuccessFeeBasisPoints = (ushort)options.SuccessFeeBasisPoints;
            response.DurationMask = 0x0F;
            response.DefaultDurationHours = (byte)AuctionRules.DefaultDurationHours;
            response.PageSize = (byte)open.PageSize;
            response.PendingMailboxCount = (ushort)open.PendingMailbox;
            response.CurrencyCount = CurrencyCount;
            for (var index = 0; index < CurrencyCount; index++)
            {
                var descriptor = response[index];
                var isZen = index == 0;
                descriptor.CurrencyMode = (MUnique.OpenMU.Network.Packets.AuctionCurrencyMode)index;
                descriptor.Enabled = isZen;
                descriptor.MinimumStartingPrice = isZen ? (uint)AuctionRules.MinimumZenPrice : 0;
                descriptor.MaximumStartingPrice = isZen ? (uint)AuctionRules.MaximumZenPrice : 0;
                descriptor.MinimumIncrement = isZen ? (uint)AuctionRules.MinimumZenBidIncrement : 0;
                descriptor.IncrementPercent = (byte)AuctionRules.BidIncrementPercent;
                descriptor.SpendableScalar = isZen ? money : 0;
                descriptor.SpendableStrength = 0;
                descriptor.SpendableAgility = 0;
                descriptor.SpendableVitality = 0;
                descriptor.SpendableEnergy = 0;
                descriptor.SpendableCommand = 0;
            }

            return response.Header.Length;
        }

        await connection.SendAsync(WritePacket).ConfigureAwait(false);
    }
}
