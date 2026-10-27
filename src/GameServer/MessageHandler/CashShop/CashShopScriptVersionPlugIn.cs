// <copyright file="CashShopScriptVersionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.CashShop;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>Tells the client which shop script to load when a character enters the game; without it the client refuses to open the shop.</summary>
[PlugIn]
[Guid("5C0E5C9A-2F3B-4F0D-9B9E-6B1F0D6B7C11")]
internal sealed class CashShopScriptVersionPlugIn : IPlayerStateChangedPlugIn
{
    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.EnteredWorld
            || player is not RemotePlayer { Connection: { } connection }
            || player.GameContext.PersistenceContextProvider.CreateCashShopRepository() is null)
        {
            return;
        }

        var packet = CashShopProtocol.ScriptVersion();
        await connection.SendAsync(() =>
        {
            packet.AsSpan().CopyTo(connection.Output.GetSpan(packet.Length));
            return packet.Length;
        }).ConfigureAwait(false);
    }
}
