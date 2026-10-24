# C3 D7 00 - ResetNpcResetRequest (by client)

## Is sent when

The player presses the Reset button in the window of the reset NPC (Raymond the Reset Master).

## Causes the following actions on the server side

The server checks the costs again and, when they are met, takes them and resets the character; the answer is a ResetNpcStateResponse.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC3  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD7  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x00  | Packet header - sub packet type identifier |