# C3 D5 09 - AuctionCollectRequest (by client)

## Is sent when

Auction House request AuctionCollectRequest (sub 09).

## Causes the following actions on the server side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC3  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   56   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x09  | Packet header - sub packet type identifier |
| 4 | 16 | Binary |  | OperationId |
| 20 | 8 | LongLittleEndian |  | CollectionId |
| 28 | 4 | IntegerLittleEndian |  | ExpectedVersion |
| 32 | 4 | IntegerLittleEndian |  | RequestedScalar |
| 36 | 4 | IntegerLittleEndian |  | RequestedStrength |
| 40 | 4 | IntegerLittleEndian |  | RequestedAgility |
| 44 | 4 | IntegerLittleEndian |  | RequestedVitality |
| 48 | 4 | IntegerLittleEndian |  | RequestedEnergy |
| 52 | 4 | IntegerLittleEndian |  | RequestedCommand |