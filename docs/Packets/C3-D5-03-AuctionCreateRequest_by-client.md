# C3 D5 03 - AuctionCreateRequest (by client)

## Is sent when

Auction House request AuctionCreateRequest (sub 03).

## Causes the following actions on the server side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC3  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   152   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x03  | Packet header - sub packet type identifier |
| 4 | 16 | Binary |  | OperationId |
| 20 | 1 | Byte |  | InventorySlot |
| 21 | 1 | AuctionCurrencyMode |  | CurrencyMode |
| 22 | 4 | IntegerLittleEndian |  | StartingScalar |
| 26 | 4 | IntegerLittleEndian |  | StartingStrength |
| 30 | 4 | IntegerLittleEndian |  | StartingAgility |
| 34 | 4 | IntegerLittleEndian |  | StartingVitality |
| 38 | 4 | IntegerLittleEndian |  | StartingEnergy |
| 42 | 4 | IntegerLittleEndian |  | StartingCommand |
| 46 | 4 | IntegerLittleEndian |  | BuyoutScalar |
| 50 | 4 | IntegerLittleEndian |  | BuyoutStrength |
| 54 | 4 | IntegerLittleEndian |  | BuyoutAgility |
| 58 | 4 | IntegerLittleEndian |  | BuyoutVitality |
| 62 | 4 | IntegerLittleEndian |  | BuyoutEnergy |
| 66 | 4 | IntegerLittleEndian |  | BuyoutCommand |
| 70 | 1 | Byte |  | DurationHours |
| 71 | 1 | Byte |  | NoteLength |
| 72 | 80 | Binary |  | Note |

### AuctionCurrencyMode Enum

Auction House currency modes.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Zen | Zen. |
| 1 | Chaos | Chaos. |
| 2 | Bless | Bless. |
| 3 | Soul | Soul. |
| 4 | Life | Life. |
| 5 | Creation | Creation. |
| 6 | Guardian | Guardian. |
| 7 | Harmony | Harmony. |
| 8 | Fruits | Fruits. |