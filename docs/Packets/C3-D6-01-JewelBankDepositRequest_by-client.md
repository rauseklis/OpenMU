# C3 D6 01 - JewelBankDepositRequest (by client)

## Is sent when

The player deposits the chosen amount of each kind from the inventory. Amounts holds 45 little-endian 32-bit counts, one per kind with code 1..45 (jewels, fruits, then the bundles of 10, 20 and 30); 0 skips a kind.

## Causes the following actions on the server side

Jewel Bank service, see docs/superpowers/specs/2026-10-20-jewel-bank-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC3  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   200   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD6  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x01  | Packet header - sub packet type identifier |
| 4 | 16 | Binary |  | OperationId |
| 20 | 180 | Binary |  | Amounts |