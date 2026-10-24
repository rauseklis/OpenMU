# C3 DC 01 - ResetNpcInfoRequest (by client)

## Is sent when

The window of the reset NPC asks for the current requirements and the character's current values again.

## Causes the following actions on the server side

The server answers with a ResetNpcStateResponse.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC3  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xDC  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x01  | Packet header - sub packet type identifier |