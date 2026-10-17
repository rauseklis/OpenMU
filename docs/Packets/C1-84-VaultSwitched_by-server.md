# C1 84 - VaultSwitched (by server)

## Is sent when

After the vault dialog was opened, or after the player requested to switch to another vault.

## Causes the following actions on the client side

The game client clears the vault window and shows which vault is active. The items of the vault follow in a separate packet.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   6   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0x84  | Packet header - packet type identifier |
| 3 | 1 | Boolean |  | Success |
| 4 | 1 | Byte |  | VaultNumber; The number of the active vault, starting with 1. |
| 5 | 1 | Byte |  | VaultCount; The number of vaults of the account. |