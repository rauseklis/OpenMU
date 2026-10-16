namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Buffers.Binary;
using System.Text;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Network;

/// <summary>
/// Sends owner-only tooltip snapshots alongside the normal effect protocol.
/// </summary>
internal static class MagicEffectDetailsPacket
{
    internal const byte Code = 0xD7;
    internal const byte Version = 2;
    internal const int HeaderLength = 13;
    internal const int MaximumLines = 16;
    internal const int MaximumTextBytes = 96;

    internal static ValueTask SendAsync(RemotePlayer player, MagicEffect effect, bool active)
    {
        if (player.Connection is not { Connected: true } connection || effect.Id is <= 0 or >= 200)
        {
            return ValueTask.CompletedTask;
        }

        var lines = active && player.Attributes is { } attributes ? MagicEffectTooltip.Build(effect, attributes) : [];
        var duration = active ? (uint)Math.Clamp(Math.Ceiling(effect.RemainingDuration.TotalSeconds), 0, uint.MaxValue) : 0;
        return connection.SendAsync(() => Write(connection.Output.GetSpan(HeaderLength + (MaximumLines * (MaximumTextBytes + 2))), effect.Id, active ? (byte)1 : (byte)0, duration, lines));
    }

    internal static ValueTask ClearAsync(RemotePlayer player) => player.Connection is { Connected: true } connection
        ? connection.SendAsync(() => Write(connection.Output.GetSpan(HeaderLength), 0, 2, 0, []))
        : ValueTask.CompletedTask;

    internal static int Write(Span<byte> buffer, short effect, byte action, uint remainingSeconds, IReadOnlyList<MagicEffectTooltipLine> lines)
    {
        var count = Math.Min(lines.Count, MaximumLines);
        buffer[0] = 0xC2;
        buffer[3] = Code;
        buffer[4] = Version;
        buffer[5] = action;
        BinaryPrimitives.WriteInt16LittleEndian(buffer[6..], effect);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[8..], remainingSeconds);
        buffer[12] = (byte)count;
        var offset = HeaderLength;
        for (var index = 0; index < count; index++)
        {
            var line = lines[index];
            buffer[offset++] = line.IsPenalty ? (byte)1 : (byte)0;
            var encoder = Encoding.UTF8.GetEncoder();
            encoder.Convert(line.Text.AsSpan(), buffer.Slice(offset + 1, MaximumTextBytes), true, out _, out var written, out _);
            buffer[offset++] = (byte)written;
            offset += written;
        }

        BinaryPrimitives.WriteUInt16BigEndian(buffer[1..], (ushort)offset);
        return offset;
    }
}
