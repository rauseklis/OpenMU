// <copyright file="CashShopProtocol.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.CashShop;

using System.Buffers.Binary;
using MUnique.OpenMU.Persistence.CashShop;

/// <summary>Existing packed MuMain Cash Shop messages; no shared packet definition is changed.</summary>
internal static class CashShopProtocol
{
    /// <summary>The existing Cash Shop group.</summary>
    internal const byte Code = 0xD2;

    /// <summary>Validates framing before handlers read a sub-code or payload.</summary>
    /// <param name="packet">The whole decrypted request.</param>
    /// <returns>Whether it is a supported, correctly sized C1 request.</returns>
    internal static bool IsValidRequest(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 4 || packet.Length > byte.MaxValue
            || packet[0] != 0xC1 || packet[1] != packet.Length || packet[2] != Code)
        {
            return false;
        }

        return packet[3] switch
        {
            0x01 => packet.Length == 4,
            0x02 => packet.Length == 5 && packet[4] <= 1,
            0x05 => packet.Length == 9,
            0x03 or 0x04 or 0x0A or 0x0B => packet.Length >= 5,
            0x08 => packet.Length == 4,
            0x09 or 0x13 => packet.Length == 8,
            _ => false,
        };
    }

    /// <summary>Builds the 45-byte balance response used by the installed client.</summary>
    /// <param name="balance">Committed integer balances.</param>
    /// <returns>Credit, prepaid and mileage in the native client's packed double fields.</returns>
    internal static byte[] Balance(CashShopBalance balance)
    {
        if (!balance.IsValid())
        {
            throw new ArgumentOutOfRangeException(nameof(balance));
        }

        var result = Create(0x01, 45);
        WriteDouble(result, 5, balance.WCoinC + balance.WCoinP);
        WriteDouble(result, 13, balance.WCoinC);
        WriteDouble(result, 21, balance.WCoinP);
        WriteDouble(result, 29, 0);
        WriteDouble(result, 37, balance.GoblinPoints);
        return result;
    }

    /// <summary>Builds an open response; zero denies opening.</summary>
    /// <param name="available">Whether persistent balances are available to the authenticated character.</param>
    /// <returns>The standard response.</returns>
    internal static byte[] Open(bool available)
    {
        var result = Create(0x02, 5);
        result[4] = available ? (byte)1 : (byte)0;
        return result;
    }

    /// <summary>Builds an empty storage page.</summary>
    /// <returns>The zero-item count packet.</returns>
    internal static byte[] EmptyStorage()
    {
        var result = Create(0x06, 12);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), 1);
        return result;
    }

    /// <summary>Refuses unsupported mutations without granting items or debiting currency.</summary>
    /// <param name="subCode">The requested operation.</param>
    /// <returns>A non-success reply, or an empty event list.</returns>
    internal static byte[] Refused(byte subCode)
    {
        var result = Create(subCode, subCode switch
        {
            0x03 => 9,
            0x04 => 17,
            0x08 => 23,
            0x09 => 12,
            0x13 => 6,
            _ => 5,
        });
        if (subCode is 0x03 or 0x04)
        {
            result[4] = 6; // The item cannot be bought/gifted.
        }
        else if (subCode is 0x0A or 0x0B)
        {
            result[4] = 1; // No such storage item.
        }

        return result;
    }

    private static byte[] Create(byte subCode, byte length)
    {
        var result = new byte[length];
        result[0] = 0xC1;
        result[1] = length;
        result[2] = Code;
        result[3] = subCode;
        return result;
    }

    private static void WriteDouble(byte[] packet, int offset, double value)
        => BinaryPrimitives.WriteInt64LittleEndian(packet.AsSpan(offset), BitConverter.DoubleToInt64Bits(value));
}
