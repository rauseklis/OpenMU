# C1 84 - VaultSwitchRequest (by client)

## Is sent when

The player wants to switch to another vault of the opened vault dialog.

## Causes the following actions on the server side

The requested vault becomes the active one and its items are sent to the player.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x84  | Packet header - packet type identifier |
| 3 | 1 | Byte |  | VaultNumber; The number of the requested vault, starting with 1. |