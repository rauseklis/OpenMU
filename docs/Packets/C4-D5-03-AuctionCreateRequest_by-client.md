# C4 D5 03 - AuctionCreateRequest (by client)

## Is sent when

Auction House request AuctionCreateRequest (sub 03).

## Causes the following actions on the server side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC4  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |   257   | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x03  | Packet header - sub packet type identifier |
| 5 | 16 | Binary |  | OperationId |
| 21 | 1 | Byte |  | InventorySlot |
| 22 | 1 | AuctionCurrencyMode |  | CurrencyMode |
| 23 | 4 | IntegerLittleEndian |  | StartingScalar |
| 27 | 4 | IntegerLittleEndian |  | StartingStrength |
| 31 | 4 | IntegerLittleEndian |  | StartingAgility |
| 35 | 4 | IntegerLittleEndian |  | StartingVitality |
| 39 | 4 | IntegerLittleEndian |  | StartingEnergy |
| 43 | 4 | IntegerLittleEndian |  | StartingCommand |
| 47 | 4 | IntegerLittleEndian |  | BuyoutScalar |
| 51 | 4 | IntegerLittleEndian |  | BuyoutStrength |
| 55 | 4 | IntegerLittleEndian |  | BuyoutAgility |
| 59 | 4 | IntegerLittleEndian |  | BuyoutVitality |
| 63 | 4 | IntegerLittleEndian |  | BuyoutEnergy |
| 67 | 4 | IntegerLittleEndian |  | BuyoutCommand |
| 71 | 1 | Byte |  | DurationHours |
| 72 | 1 | Byte |  | NoteLength |
| 73 | 80 | Binary |  | Note |
| 153 | 52 | Binary |  | StartingBasket |
| 205 | 52 | Binary |  | BuyoutBasket |

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
| 9 | Mixed | A basket of several currencies, or a buy-now price. |