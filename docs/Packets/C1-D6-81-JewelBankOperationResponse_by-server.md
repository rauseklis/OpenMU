# C1 D6 81 - JewelBankOperationResponse (by server)

## Is sent when

Result of a deposit, withdrawal or setting change.

## Causes the following actions on the client side

Jewel Bank service, see docs/superpowers/specs/2026-10-20-jewel-bank-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   27   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD6  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x81  | Packet header - sub packet type identifier |
| 4 | 16 | Binary |  | OperationId |
| 20 | 1 | Byte |  | Operation |
| 21 | 1 | JewelBankResult |  | Result |
| 22 | 1 | Byte |  | KindCode |
| 23 | 4 | IntegerLittleEndian |  | Units |

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