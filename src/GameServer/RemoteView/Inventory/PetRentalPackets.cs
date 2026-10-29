// <copyright file="PetRentalPackets.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Inventory;

using System.Buffers.Binary;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Network;

/// <summary>Uses the existing S6 client D2:11/12 period-item countdown protocol; no new packet contract.</summary>
internal static class PetRentalPackets
{
    internal static byte[] ItemPacket(Item item)
    {
        var packet = new byte[12];
        packet[0] = 0xC1;
        packet[1] = 12;
        packet[2] = 0xD2;
        packet[3] = 0x12;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(4), (ushort)((item.Definition!.Group * 512) + item.Definition.Number));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(6), item.ItemSlot);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(8), checked((int)item.RentalExpiresAt!.Value.ToUnixTimeSeconds()));
        return packet;
    }

    internal static async ValueTask SendAsync(RemotePlayer player)
    {
        var connection = player.Connection;
        if (connection is null)
        {
            return;
        }

        var items = player.Inventory?.Items.Where(i => i.RentalExpiresAt.HasValue).ToList() ?? [];
        var count = new byte[] { 0xC1, 5, 0xD2, 0x11, (byte)items.Count };
        foreach (var packet in new[] { count }.Concat(items.Select(ItemPacket)))
        {
            await connection.SendAsync(() =>
            {
                packet.CopyTo(connection.Output.GetSpan(packet.Length));
                return packet.Length;
            }).ConfigureAwait(false);
        }
    }
}
