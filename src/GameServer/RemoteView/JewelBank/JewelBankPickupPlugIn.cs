// <copyright file="JewelBankPickupPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.JewelBank;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Views.JewelBank;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IJewelBankPickupPlugIn"/> which forwards the notification to the game client.
/// </summary>
[PlugIn]
[Display(Name = "Jewel Bank pickup notification", Description = "Tells the client that a picked-up item went straight into the Jewel Bank.")]
[Guid("B8712671-17DA-48C0-B2B0-C2331AA40020")]
public class JewelBankPickupPlugIn : IJewelBankPickupPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="JewelBankPickupPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public JewelBankPickupPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask JewelBankPickupAsync(byte kindCode, uint units, ulong newCount)
    {
        await this._player.Connection.SendJewelBankPickupNotificationAsync(kindCode, units, newCount).ConfigureAwait(false);
    }
}
