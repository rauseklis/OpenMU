// <copyright file="VaultSwitchHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Vault;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Vault;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handler for vault switch packets.
/// </summary>
[PlugIn]
[Display(Name = "Vault switch handler", Description = "Handles the packets which request to switch to another vault.")]
[Guid("A2D8B3C6-4F1E-4B7D-8E55-9C3A17D6B0F4")]
internal class VaultSwitchHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly SwitchVaultAction _switchAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => VaultSwitchRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        VaultSwitchRequest request = packet;
        await this._switchAction.SwitchVaultAsync(player, request.VaultNumber).ConfigureAwait(false);
    }
}
