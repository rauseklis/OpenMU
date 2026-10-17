// <copyright file="UpdateVaultNumberPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Vault;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.GameLogic.Views.Vault;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IUpdateVaultNumberPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = "Update vault number", Description = "Tells the game client which vault is currently active.")]
[Guid("5C0F3D52-6B7A-4E6B-9C1D-2B8A64F1E0A7")]
public class UpdateVaultNumberPlugIn : IUpdateVaultNumberPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateVaultNumberPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public UpdateVaultNumberPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask UpdateVaultNumberAsync(bool success)
    {
        await this._player.Connection.SendVaultSwitchedAsync(success, this._player.ActiveVaultNumber, (byte)InventoryConstants.VaultCount).ConfigureAwait(false);
    }
}
