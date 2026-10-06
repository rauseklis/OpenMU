# C3 D5 08 - AuctionMailboxRequest (by client)

## Is sent when

Auction House request AuctionMailboxRequest (sub 08).

## Causes the following actions on the server side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC3  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   11   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x08  | Packet header - sub packet type identifier |
| 4 | 4 | IntegerLittleEndian |  | RequestId |
| 8 | 2 | ShortLittleEndian |  | Page |
| 10 | 1 | Byte |  | KindFilter |