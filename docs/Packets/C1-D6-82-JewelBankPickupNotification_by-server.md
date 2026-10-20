# C1 D6 82 - JewelBankPickupNotification (by server)

## Is sent when

An item picked up by the player went straight into the bank (auto-deposit).

## Causes the following actions on the client side

Jewel Bank service, see docs/superpowers/specs/2026-10-20-jewel-bank-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   17   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD6  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x82  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | KindCode |
| 5 | 4 | IntegerLittleEndian |  | Units |
| 9 | 8 | LongLittleEndian |  | NewCount |