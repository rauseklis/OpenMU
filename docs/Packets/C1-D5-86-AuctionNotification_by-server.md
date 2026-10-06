# C1 D5 86 - AuctionNotification (by server)

## Is sent when

Real-time Auction House notification.

## Causes the following actions on the client side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   40   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x86  | Packet header - sub packet type identifier |
| 4 | 1 | AuctionNotificationKind |  | NotificationKind |
| 5 | 8 | LongLittleEndian |  | ListingId |
| 13 | 1 | AuctionCurrencyMode |  | CurrencyMode |
| 14 | 4 | IntegerLittleEndian |  | AmountScalar |
| 18 | 4 | IntegerLittleEndian |  | AmountStrength |
| 22 | 4 | IntegerLittleEndian |  | AmountAgility |
| 26 | 4 | IntegerLittleEndian |  | AmountVitality |
| 30 | 4 | IntegerLittleEndian |  | AmountEnergy |
| 34 | 4 | IntegerLittleEndian |  | AmountCommand |
| 38 | 2 | ShortLittleEndian |  | PendingMailboxCount |

### AuctionNotificationKind Enum

Auction House notification kinds.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Outbid | Outbid. |
| 1 | Sold | Sold. |
| 2 | Bought | Bought. |
| 3 | Expired | Expired. |
| 4 | Returned | Returned. |
| 5 | Collected | Collected. |
| 6 | AdminAdjusted | AdminAdjusted. |

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