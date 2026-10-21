# C2 D6 80 - JewelBankStateResponse (by server)

## Is sent when

The stored counts of the account and the number of items of each kind the player carries in the inventory (what a deposit could take): answer to an open request, and after every change. Counts and Inventory hold 45 little-endian 32-bit values, one per kind with code 1..45.

## Causes the following actions on the client side

Jewel Bank service, see docs/superpowers/specs/2026-10-20-jewel-bank-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |   367   | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xD6  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x80  | Packet header - sub packet type identifier |
| 5 | 1 | JewelBankResult |  | Result |
| 6 | 1 | Byte |  | AutoDeposit |
| 7 | 180 | Binary |  | Counts |
| 187 | 180 | Binary |  | Inventory |

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