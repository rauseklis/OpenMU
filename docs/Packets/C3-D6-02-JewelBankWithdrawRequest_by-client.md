# C3 D6 02 - JewelBankWithdrawRequest (by client)

## Is sent when

The player withdraws the chosen amount of each kind as items; Amount1..Amount15 are the units for the kinds with code 1..15, and 0 skips a kind.

## Causes the following actions on the server side

Jewel Bank service, see docs/superpowers/specs/2026-10-20-jewel-bank-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC3  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   80   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD6  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x02  | Packet header - sub packet type identifier |
| 4 | 16 | Binary |  | OperationId |
| 20 | 4 | IntegerLittleEndian |  | Amount1 |
| 24 | 4 | IntegerLittleEndian |  | Amount2 |
| 28 | 4 | IntegerLittleEndian |  | Amount3 |
| 32 | 4 | IntegerLittleEndian |  | Amount4 |
| 36 | 4 | IntegerLittleEndian |  | Amount5 |
| 40 | 4 | IntegerLittleEndian |  | Amount6 |
| 44 | 4 | IntegerLittleEndian |  | Amount7 |
| 48 | 4 | IntegerLittleEndian |  | Amount8 |
| 52 | 4 | IntegerLittleEndian |  | Amount9 |
| 56 | 4 | IntegerLittleEndian |  | Amount10 |
| 60 | 4 | IntegerLittleEndian |  | Amount11 |
| 64 | 4 | IntegerLittleEndian |  | Amount12 |
| 68 | 4 | IntegerLittleEndian |  | Amount13 |
| 72 | 4 | IntegerLittleEndian |  | Amount14 |
| 76 | 4 | IntegerLittleEndian |  | Amount15 |