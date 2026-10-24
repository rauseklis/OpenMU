// <copyright file="ShowResetNpcPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Reset;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.GameLogic.Views.Reset;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowResetNpcPlugIn"/> which sends the status to the game client.
/// </summary>
[PlugIn]
[Display(Name = "Reset NPC window", Description = "Shows the window of the reset NPC with the requirements of the next reset.")]
[Guid("3C6E8B52-91D4-4F07-A2B5-6D1E0F7A9C48")]
public class ShowResetNpcPlugIn : IShowResetNpcPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowResetNpcPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowResetNpcPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowResetNpcAsync(ResetNpcStatus status)
    {
        await this._player.Connection.SendResetNpcStateResponseAsync(
            (byte)status.Outcome,
            status.Flags,
            (ushort)Math.Clamp(status.NextResetNumber, 0, ushort.MaxValue),
            (ushort)Math.Clamp(status.MaximumResets, 0, ushort.MaxValue),
            (ushort)Math.Clamp(status.RequiredLevel, 0, ushort.MaxValue),
            (ushort)Math.Clamp(status.CurrentLevel, 0, ushort.MaxValue),
            (uint)Math.Max(0, status.RequiredZen),
            (uint)Math.Max(0, status.CurrentZen),
            (uint)Math.Max(0, status.RequiredFruits),
            (uint)Math.Clamp(status.BankFruits, 0, uint.MaxValue)).ConfigureAwait(false);
    }
}
