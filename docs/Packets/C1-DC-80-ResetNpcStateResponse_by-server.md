# C1 DC 80 - ResetNpcStateResponse (by server)

## Is sent when

The player talks to the reset NPC (Raymond the Reset Master), asks for the information again or presses Reset. Outcome: 0 = the information for the window, 1 = the reset was done, 2 = the requirements are not met, 3 = the reset limit is reached, 4 = the request was refused (state, rate limit or the NPC is not open). Flags: bit 0 = the level is enough, bit 1 = the Zen is enough, bit 2 = the fruits are enough, bit 3 = the reset limit is reached, bit 4 = nothing is worn (or the equipment rule does not apply), bit 5 = the reset requires that nothing is worn. WornItems is the number of items the character wears.

## Causes the following actions on the client side

The client shows or updates the window of the reset NPC.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   31   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xDC  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x80  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Outcome |
| 5 | 1 | Byte |  | Flags |
| 6 | 2 | ShortLittleEndian |  | NextResetNumber |
| 8 | 2 | ShortLittleEndian |  | MaximumResets |
| 10 | 2 | ShortLittleEndian |  | RequiredLevel |
| 12 | 2 | ShortLittleEndian |  | CurrentLevel |
| 14 | 4 | IntegerLittleEndian |  | RequiredZen |
| 18 | 4 | IntegerLittleEndian |  | CurrentZen |
| 22 | 4 | IntegerLittleEndian |  | RequiredFruits |
| 26 | 4 | IntegerLittleEndian |  | BankFruits |
| 30 | 1 | Byte |  | WornItems |