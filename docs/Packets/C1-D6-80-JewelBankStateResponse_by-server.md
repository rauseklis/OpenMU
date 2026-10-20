# C1 D6 80 - JewelBankStateResponse (by server)

## Is sent when

The stored counts of the account: answer to an open request, and after every change. Count1..Count15 are the kinds with code 1..15.

## Causes the following actions on the client side

Jewel Bank service, see docs/superpowers/specs/2026-10-20-jewel-bank-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   126   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD6  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x80  | Packet header - sub packet type identifier |
| 4 | 1 | JewelBankResult |  | Result |
| 5 | 1 | Byte |  | AutoDeposit |
| 6 | 8 | LongLittleEndian |  | Count1 |
| 14 | 8 | LongLittleEndian |  | Count2 |
| 22 | 8 | LongLittleEndian |  | Count3 |
| 30 | 8 | LongLittleEndian |  | Count4 |
| 38 | 8 | LongLittleEndian |  | Count5 |
| 46 | 8 | LongLittleEndian |  | Count6 |
| 54 | 8 | LongLittleEndian |  | Count7 |
| 62 | 8 | LongLittleEndian |  | Count8 |
| 70 | 8 | LongLittleEndian |  | Count9 |
| 78 | 8 | LongLittleEndian |  | Count10 |
| 86 | 8 | LongLittleEndian |  | Count11 |
| 94 | 8 | LongLittleEndian |  | Count12 |
| 102 | 8 | LongLittleEndian |  | Count13 |
| 110 | 8 | LongLittleEndian |  | Count14 |
| 118 | 8 | LongLittleEndian |  | Count15 |

### JewelBankResult Enum

Jewel Bank result codes.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Success | Success. |
| 1 | InvalidRequest | InvalidRequest. |
| 2 | InsufficientBalance | InsufficientBalance. |
| 3 | InventoryFull | InventoryFull. |
| 4 | CapReached | CapReached. |
| 5 | NothingToDeposit | NothingToDeposit. |
| 6 | NotConfigured | NotConfigured. |
| 7 | StateRestricted | StateRestricted. |
| 8 | RateLimited | RateLimited. |
| 9 | ServiceUnavailable | ServiceUnavailable. |