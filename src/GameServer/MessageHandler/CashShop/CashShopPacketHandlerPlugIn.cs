// <copyright file="CashShopPacketHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.CashShop;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>Handles the installed MuMain client's existing Cash Shop request group.</summary>
[PlugIn]
[Guid("7929DC66-A4C1-4FD9-B7D2-E57875F08A2B")]
internal sealed class CashShopPacketHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc />
    public byte Key => CashShopProtocol.Code;

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player is not RemotePlayer { Connection: { } connection }
            || !CashShopProtocol.IsValidRequest(packet.Span))
        {
            return;
        }

        var repository = player.GameContext.PersistenceContextProvider.CreateCashShopRepository();
        var response = await CashShopRequests.HandleAsync(player, packet, repository).ConfigureAwait(false);
        if (response is null)
        {
            return;
        }

        await connection.SendAsync(() =>
        {
            response.AsSpan().CopyTo(connection.Output.GetSpan(response.Length));
            return response.Length;
        }).ConfigureAwait(false);
    }
}
