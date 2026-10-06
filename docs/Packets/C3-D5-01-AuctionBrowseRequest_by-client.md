# C3 D5 01 - AuctionBrowseRequest (by client)

## Is sent when

Auction House request AuctionBrowseRequest (sub 01).

## Causes the following actions on the server side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC3  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   104   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x01  | Packet header - sub packet type identifier |
| 4 | 4 | IntegerLittleEndian |  | RequestId |
| 8 | 2 | ShortLittleEndian |  | Page |
| 10 | 1 | Byte |  | Category |
| 11 | 1 | AuctionCurrencyMode |  | CurrencyMode |
| 12 | 1 | AuctionSort |  | Sort |
| 13 | 4 | IntegerLittleEndian |  | ClassMask |
| 17 | 1 | Byte |  | LevelMinimum |
| 18 | 1 | Byte |  | LevelMaximum |
| 19 | 2 | ShortLittleEndian |  | OptionFlags |
| 21 | 4 | IntegerLittleEndian |  | MinimumScalar |
| 25 | 4 | IntegerLittleEndian |  | MinimumStrength |
| 29 | 4 | IntegerLittleEndian |  | MinimumAgility |
| 33 | 4 | IntegerLittleEndian |  | MinimumVitality |
| 37 | 4 | IntegerLittleEndian |  | MinimumEnergy |
| 41 | 4 | IntegerLittleEndian |  | MinimumCommand |
| 45 | 4 | IntegerLittleEndian |  | MaximumScalar |
| 49 | 4 | IntegerLittleEndian |  | MaximumStrength |
| 53 | 4 | IntegerLittleEndian |  | MaximumAgility |
| 57 | 4 | IntegerLittleEndian |  | MaximumVitality |
| 61 | 4 | IntegerLittleEndian |  | MaximumEnergy |
| 65 | 4 | IntegerLittleEndian |  | MaximumCommand |
| 69 | 2 | ShortLittleEndian |  | MaxRemainingHours |
| 71 | 1 | Byte |  | NameLength |
| 72 | 32 | Binary |  | Name |

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

### AuctionSort Enum

Auction House sort orders.

| Value | Name | Description |
|-------|------|-------------|
| 0 | PriceAscending | PriceAscending. |
| 1 | PriceDescending | PriceDescending. |
| 2 | EndingSoonest | EndingSoonest. |
| 3 | Newest | Newest. |