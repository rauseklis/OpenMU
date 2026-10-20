# C1 D6 02 - JewelBankWithdrawRequest (by client)

## Is sent when

The player withdraws units of one kind as items.

## Causes the following actions on the server side

Jewel Bank service, see docs/superpowers/specs/2026-10-20-jewel-bank-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   25   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD6  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x02  | Packet header - sub packet type identifier |
| 4 | 16 | Binary |  | OperationId |
| 20 | 1 | Byte |  | KindCode |
| 21 | 4 | IntegerLittleEndian |  | Units |