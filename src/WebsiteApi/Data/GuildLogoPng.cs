// <copyright file="GuildLogoPng.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Data;

using System.Buffers.Binary;
using System.IO.Compression;

/// <summary>
/// Converts the logo of a guild (8x8 pixels, 16 colors, two pixels per byte, the high nibble first, row by row)
/// into a PNG which is enlarged to 64x64 pixels without smoothing. The palette is the one of the game client
/// (<c>CreateGuildMark</c>); color 0 is transparent.
/// </summary>
public static class GuildLogoPng
{
    /// <summary>The size of the stored logo in bytes.</summary>
    public const int LogoSize = 32;

    private const int Scale = 8;
    private const int Pixels = 8;

    private static readonly (byte R, byte G, byte B, byte A)[] Palette =
    {
        (0, 0, 0, 0), (0, 0, 0, 255), (128, 128, 128, 255), (255, 255, 255, 255),
        (255, 0, 0, 255), (255, 128, 0, 255), (255, 255, 0, 255), (128, 255, 0, 255),
        (0, 255, 0, 255), (0, 255, 128, 255), (0, 255, 255, 255), (0, 128, 255, 255),
        (0, 0, 255, 255), (128, 0, 255, 255), (255, 0, 255, 255), (255, 0, 128, 255),
    };

    /// <summary>
    /// Creates the PNG.
    /// </summary>
    /// <param name="logo">The raw logo of 32 bytes.</param>
    /// <returns>The PNG file content, or <c>null</c> when the logo is missing or has the wrong size.</returns>
    public static byte[]? Create(byte[]? logo)
    {
        if (logo is null || logo.Length != LogoSize)
        {
            return null;
        }

        const int Size = Pixels * Scale;
        var raw = new byte[Size * ((Size * 4) + 1)];
        var position = 0;
        for (var y = 0; y < Size; y++)
        {
            raw[position++] = 0; // filter: none
            var row = y / Scale;
            for (var x = 0; x < Size; x++)
            {
                var index = (row * Pixels) + (x / Scale);
                var value = logo[index / 2];
                var color = Palette[index % 2 == 0 ? value >> 4 : value & 0x0F];
                raw[position++] = color.R;
                raw[position++] = color.G;
                raw[position++] = color.B;
                raw[position++] = color.A;
            }
        }

        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), Size);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), Size);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        WriteChunk(output, "IHDR", header);

        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(raw);
            }

            WriteChunk(output, "IDAT", compressed.ToArray());
        }

        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = Crc32(typeBytes, 0xFFFFFFFF);
        crc = Crc32(data, crc) ^ 0xFFFFFFFF;
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }

    private static uint Crc32(byte[] data, uint crc)
    {
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return crc;
    }
}
