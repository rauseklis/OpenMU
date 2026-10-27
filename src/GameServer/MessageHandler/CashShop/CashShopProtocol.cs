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

    /// <summary>The number of storage items the client shows per page.</summary>
    internal const int StoragePageSize = 9;

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
            0x05 => packet.Length is 9 or 10,
            0x03 => packet.Length == 23,
            0x0B => packet.Length == 15,
            0x04 or 0x0A => packet.Length >= 5,
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

    /// <summary>Builds the answer to a buy request.</summary>
    /// <param name="result">0 = bought; 1 = not enough currency; 2 = storage full; 4 = not available; 0xFF = error.</param>
    /// <returns>The 9-byte answer.</returns>
    internal static byte[] BuyResult(byte result)
    {
        var packet = Create(0x03, 9);
        packet[4] = result;
        return packet;
    }

    /// <summary>Builds the answer to a use request.</summary>
    /// <param name="result">0 = used; 1 = no such item; 21 = inventory full; 22 = cannot be used; 0xFF = error.</param>
    /// <returns>The 5-byte answer.</returns>
    internal static byte[] UseResult(byte result)
    {
        var packet = Create(0x0B, 5);
        packet[4] = result;
        return packet;
    }

    /// <summary>Builds the answer to a delete request.</summary>
    /// <param name="result">0 = deleted; otherwise failed.</param>
    /// <returns>The 5-byte answer.</returns>
    internal static byte[] DeleteResult(byte result)
    {
        var packet = Create(0x0A, 5);
        packet[4] = result;
        return packet;
    }

    /// <summary>Builds the announcement of the shop script version; the client cannot open the shop before it got one.</summary>
    /// <returns>The 10-byte packet.</returns>
    internal static byte[] ScriptVersion()
    {
        var packet = Create(0x0C, 10);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(4), CashShopCatalog.ScriptZone);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(6), CashShopCatalog.ScriptYear);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), CashShopCatalog.ScriptYearIdentifier);
        return packet;
    }

    /// <summary>Builds one page of the storage: the count packet, followed by one packet per item.</summary>
    /// <param name="items">All stored items of the account.</param>
    /// <param name="page">The requested, 1-based page.</param>
    /// <returns>The packets to send in order.</returns>
    internal static IReadOnlyList<byte[]> StoragePage(IReadOnlyList<CashShopStorageItem> items, int page)
    {
        var totalPages = (items.Count + StoragePageSize - 1) / StoragePageSize;
        page = Math.Clamp(page, 1, Math.Max(1, totalPages));
        var pageItems = items.Skip((page - 1) * StoragePageSize).Take(StoragePageSize).ToList();
        var count = Create(0x06, 12);
        BinaryPrimitives.WriteUInt16LittleEndian(count.AsSpan(4), (ushort)Math.Min(items.Count, ushort.MaxValue));
        BinaryPrimitives.WriteUInt16LittleEndian(count.AsSpan(6), (ushort)pageItems.Count);
        BinaryPrimitives.WriteUInt16LittleEndian(count.AsSpan(8), (ushort)page);
        BinaryPrimitives.WriteUInt16LittleEndian(count.AsSpan(10), (ushort)totalPages);
        var result = new List<byte[]> { count };
        foreach (var item in pageItems)
        {
            var entry = Create(0x0D, 33);
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(4), unchecked((int)item.Id));
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(8), unchecked((int)item.Id));
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(12), 0);
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(16), item.ProductSeq);
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(20), item.PriceSeq);
            entry[32] = (byte)'P';
            result.Add(entry);
        }

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
