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
/// spendable currency inventory and the pending Mailbox count.
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
                : !AuctionHouseMutations.RateLimiter.TryAcquire(who.AccountId, AuctionRequestCost.Query)
                    ? new AuctionOpenResult(AuctionResultCode.RateLimited, 0, now, options.PageSize)
                    : await service.OpenAsync(who.AccountId).ConfigureAwait(false);
        var money = (uint)Math.Max(0, player.SelectedCharacter?.Inventory?.Money ?? 0);
        var inventoryItems = player.SelectedCharacter?.Inventory?.Items ?? [];
        var configuration = player.GameContext.Configuration;
        var jewelSnapshots = Enumerable.Range(1, 7)
            .Select(index => AuctionCurrencyInventorySnapshot.CreateJewel((byte)index, configuration.JewelMixes, inventoryItems))
            .ToArray();
        var fruitSnapshot = AuctionFruitInventorySnapshot.Create(configuration.Items, inventoryItems);

        int WritePacket()
        {
            var length = AuctionOpenResponseRef.GetRequiredSize(CurrencyCount);
            var response = new AuctionOpenResponseRef(connection.Output.GetSpan(length)[..length]);
            response.RequestId = request.RequestId;
            response.Result = (AuctionResult)open.Result;
            response.ServerTime = (uint)open.ServerTime.ToUnixTimeSeconds();
            response.ConfigurationVersion = 4;
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
                var isJewel = index is >= (int)MUnique.OpenMU.GameLogic.AuctionHouse.AuctionCurrencyMode.Chaos
                    and <= (int)MUnique.OpenMU.GameLogic.AuctionHouse.AuctionCurrencyMode.Harmony;
                var isFruits = index == (int)MUnique.OpenMU.GameLogic.AuctionHouse.AuctionCurrencyMode.Fruits;
                var jewel = isJewel ? jewelSnapshots[index - 1] : null;
                descriptor.CurrencyMode = (MUnique.OpenMU.Network.Packets.AuctionCurrencyMode)index;
                descriptor.Enabled = isZen || jewel?.IsConfigured == true || (isFruits && fruitSnapshot.IsConfigured);
                descriptor.MinimumStartingPrice = isZen
                    ? (uint)AuctionRules.MinimumZenPrice
                    : isJewel ? (uint)AuctionRules.MinimumJewelPrice : 0;
                descriptor.MaximumStartingPrice = isZen
                    ? (uint)AuctionRules.MaximumZenPrice
                    : isJewel ? (uint)AuctionRules.MaximumJewelPrice : 0;
                descriptor.MinimumIncrement = isZen
                    ? (uint)AuctionRules.MinimumZenBidIncrement
                    : isJewel ? (uint)AuctionRules.MinimumJewelBidIncrement : 0;
                descriptor.IncrementPercent = (byte)AuctionRules.BidIncrementPercent;
                descriptor.SpendableScalar = isZen ? money : jewel?.TotalUnits ?? 0;
                descriptor.SpendableStrength = isFruits ? Clamp(fruitSnapshot.Spendable.Strength) : 0;
                descriptor.SpendableAgility = isFruits ? Clamp(fruitSnapshot.Spendable.Agility) : 0;
                descriptor.SpendableVitality = isFruits ? Clamp(fruitSnapshot.Spendable.Vitality) : 0;
                descriptor.SpendableEnergy = isFruits ? Clamp(fruitSnapshot.Spendable.Energy) : 0;
                descriptor.SpendableCommand = isFruits ? Clamp(fruitSnapshot.Spendable.Command) : 0;
                descriptor.SmallPackUnits = isJewel ? (ushort)JewelPackRules.GetPackUnits(0) : (ushort)0;
                descriptor.MediumPackUnits = isJewel ? (ushort)JewelPackRules.GetPackUnits(1) : (ushort)0;
                descriptor.LargePackUnits = isJewel ? (ushort)JewelPackRules.GetPackUnits(2) : (ushort)0;
                descriptor.LooseItemType = jewel?.LooseItemType ?? 0;
                descriptor.PackItemType = jewel?.PackItemType ?? 0;
                descriptor.LooseItemCount = jewel?.LooseCount ?? 0;
                descriptor.SmallPackCount = jewel?.PackCounts[0] ?? 0;
                descriptor.MediumPackCount = jewel?.PackCounts[1] ?? 0;
                descriptor.LargePackCount = jewel?.PackCounts[2] ?? 0;
                descriptor.StrengthFruitItemType = isFruits ? fruitSnapshot.FruitItemType : (ushort)0;
                descriptor.AgilityFruitItemType = isFruits ? fruitSnapshot.FruitItemType : (ushort)0;
                descriptor.VitalityFruitItemType = isFruits ? fruitSnapshot.FruitItemType : (ushort)0;
                descriptor.EnergyFruitItemType = isFruits ? fruitSnapshot.FruitItemType : (ushort)0;
                descriptor.CommandFruitItemType = isFruits ? fruitSnapshot.FruitItemType : (ushort)0;
                descriptor.StrengthFruitItemLevel = isFruits ? (byte)FruitRules.GetItemLevel(FruitComponent.Strength) : (byte)0;
                descriptor.AgilityFruitItemLevel = isFruits ? (byte)FruitRules.GetItemLevel(FruitComponent.Agility) : (byte)0;
                descriptor.VitalityFruitItemLevel = isFruits ? (byte)FruitRules.GetItemLevel(FruitComponent.Vitality) : (byte)0;
                descriptor.EnergyFruitItemLevel = isFruits ? (byte)FruitRules.GetItemLevel(FruitComponent.Energy) : (byte)0;
                descriptor.CommandFruitItemLevel = isFruits ? (byte)FruitRules.GetItemLevel(FruitComponent.Command) : (byte)0;
            }

            return response.Header.Length;
        }

        await connection.SendAsync(WritePacket).ConfigureAwait(false);
    }

    private static uint Clamp(long value) => (uint)Math.Min(Math.Max(0, value), uint.MaxValue);
}
