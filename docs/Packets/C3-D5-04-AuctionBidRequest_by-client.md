# C3 D5 04 - AuctionBidRequest (by client)

## Is sent when

Auction House request AuctionBidRequest (sub 04).

## Causes the following actions on the server side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC3  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   56   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x04  | Packet header - sub packet type identifier |
| 4 | 16 | Binary |  | OperationId |
| 20 | 8 | LongLittleEndian |  | ListingId |
| 28 | 4 | IntegerLittleEndian |  | ExpectedVersion |
| 32 | 4 | IntegerLittleEndian |  | BidScalar |
| 36 | 4 | IntegerLittleEndian |  | BidStrength |
| 40 | 4 | IntegerLittleEndian |  | BidAgility |
| 44 | 4 | IntegerLittleEndian |  | BidVitality |
| 48 | 4 | IntegerLittleEndian |  | BidEnergy |
| 52 | 4 | IntegerLittleEndian |  | BidCommand |