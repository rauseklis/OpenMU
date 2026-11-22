// <copyright file="ClientIntegrityReportHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for the report in which the client tells which build it is and a fingerprint of its program files.
/// The report is only recorded; it changes nothing in the game.
/// </summary>
[PlugIn]
[Display(Name = "Client integrity report", Description = "Records what the client reports about its build and program files.")]
[Guid("5E2A9C41-7B38-4D06-A1F4-C3D80B6E5972")]
internal class ClientIntegrityReportHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => ClientIntegrityReport.Code;

    /// <inheritdoc/>
    public ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        try
        {
            ClientIntegrityReport message = packet;
            var build = Encoding.ASCII.GetString(message.Build).TrimEnd('\0');
            var hash = Convert.ToHexString(message.ProgramHash).ToLowerInvariant();
            player.RecordClientIntegrity(build, hash);
        }
        catch (Exception ex)
        {
            player.Logger.LogDebug(ex, "Could not record the client integrity report.");
        }

        return ValueTask.CompletedTask;
    }
}
