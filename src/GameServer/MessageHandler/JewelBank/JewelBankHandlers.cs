// <copyright file="JewelBankHandlers.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.JewelBank;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.JewelBank;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Persistence.JewelBank;
using MUnique.OpenMU.PlugIns;
using Wire = MUnique.OpenMU.Network.Packets.ClientToServer;

/// <summary>
/// Handles requests to open the Jewel Bank window: answers with the stored counts and the auto-deposit choice.
/// </summary>
[PlugIn]
[Guid("5BA9D70C-5752-4690-AE0E-C23A01CE7091")]
[BelongsToGroup(JewelBankGroupHandlerPlugIn.GroupKey)]
internal sealed class JewelBankOpenHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.JewelBankOpenRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.JewelBankOpenRequest.Length)
        {
            return;
        }

        var service = JewelBankService.Create(player);
        if (JewelBankService.GetIdentity(player) is not { } who || service is null)
        {
            await JewelBankMessages.SendStateAsync(player, JewelBankResultCode.ServiceUnavailable, new JewelBankState(new Dictionary<byte, long>(), false)).ConfigureAwait(false);
            return;
        }

        await JewelBankMessages.SendStateAsync(player, JewelBankResultCode.Success, await service.GetStateAsync(who.AccountId).ConfigureAwait(false)).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles requests to deposit the chosen amounts of jewels and fruits from the inventory.
/// </summary>
[PlugIn]
[Guid("57911B6F-3F30-4D94-9B6F-171DCF6D5A4E")]
[BelongsToGroup(JewelBankGroupHandlerPlugIn.GroupKey)]
internal sealed class JewelBankDepositHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.JewelBankDepositRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.JewelBankDepositRequest.Length)
        {
            return;
        }

        var request = new Wire.JewelBankDepositRequest(packet);
        var requested = JewelBankMessages.ToAmounts(request.Amounts);
        await JewelBankMessages.RunAsync(
            player,
            request.OperationId.ToArray(),
            JewelBankMessages.OperationKind.Deposit,
            (service, operationId) => service.DepositAsync(player, operationId, requested)).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles requests to withdraw the chosen amounts as items.
/// </summary>
[PlugIn]
[Guid("FBD8F70E-7C09-4547-9D28-2869CF35762F")]
[BelongsToGroup(JewelBankGroupHandlerPlugIn.GroupKey)]
internal sealed class JewelBankWithdrawHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.JewelBankWithdrawRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.JewelBankWithdrawRequest.Length)
        {
            return;
        }

        var request = new Wire.JewelBankWithdrawRequest(packet);
        var units = JewelBankMessages.ToAmounts(request.Amounts);
        await JewelBankMessages.RunAsync(
            player,
            request.OperationId.ToArray(),
            JewelBankMessages.OperationKind.Withdraw,
            (service, operationId) => service.WithdrawAsync(player, operationId, units)).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles requests to switch the auto-deposit of picked-up jewels and fruits on or off.
/// </summary>
[PlugIn]
[Guid("94113DAD-7F07-49B0-AEDF-7AF1B81F61DA")]
[BelongsToGroup(JewelBankGroupHandlerPlugIn.GroupKey)]
internal sealed class JewelBankSetAutoDepositHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.JewelBankSetAutoDepositRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.JewelBankSetAutoDepositRequest.Length)
        {
            return;
        }

        var enabled = new Wire.JewelBankSetAutoDepositRequest(packet).Enabled != 0;
        var service = JewelBankService.Create(player);
        if (JewelBankService.GetIdentity(player) is not { } who || service is null)
        {
            return;
        }

        if (!JewelBankService.RateLimiter.TryAcquire(who.AccountId))
        {
            await JewelBankMessages.SendStateAsync(player, JewelBankResultCode.RateLimited, await service.GetStateAsync(who.AccountId).ConfigureAwait(false)).ConfigureAwait(false);
            return;
        }

        await service.SetAutoDepositAsync(who.AccountId, enabled).ConfigureAwait(false);
        await JewelBankMessages.SendStateAsync(player, JewelBankResultCode.Success, await service.GetStateAsync(who.AccountId).ConfigureAwait(false)).ConfigureAwait(false);
    }
}
