// <copyright file="PingHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AntiCheat;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for the heartbeat ("ping") which the client sends every few seconds.
/// It only records when it arrived; it never answers and never punishes.
/// </summary>
[PlugIn]
[Display(Name = "Client heartbeat", Description = "Records the heartbeat packets of the client.")]
[Guid("C2D8F4A1-6B3E-4D75-A9E0-7F1B5C3D8E26")]
internal class PingHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => PingRef.Code;

    /// <inheritdoc/>
    public ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Ping.Length)
        {
            return ValueTask.CompletedTask;
        }

        if (player is RemotePlayer remotePlayer)
        {
            Ping message = packet;
            remotePlayer.RecordPing(message.TickCount);
            AntiCheatState.PingsReceived.Add(1);
        }

        return ValueTask.CompletedTask;
    }
}
