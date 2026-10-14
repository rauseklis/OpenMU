# C1 D5 83 - AuctionOperationResponse (by server)

## Is sent when

Result of a mutation.

## Causes the following actions on the client side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   143   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x83  | Packet header - sub packet type identifier |
| 4 | 16 | Binary |  | OperationId |
| 20 | 1 | AuctionOperationType |  | OperationType |
| 21 | 1 | AuctionResult |  | Result |
| 22 | 8 | LongLittleEndian |  | ListingId |
| 30 | 8 | LongLittleEndian |  | CollectionId |
| 38 | 4 | IntegerLittleEndian |  | ListingVersion |
| 42 | 1 | AuctionCurrencyMode |  | CurrencyMode |
| 43 | 4 | IntegerLittleEndian |  | FeeScalar |
| 47 | 4 | IntegerLittleEndian |  | FeeStrength |
| 51 | 4 | IntegerLittleEndian |  | FeeAgility |
| 55 | 4 | IntegerLittleEndian |  | FeeVitality |
| 59 | 4 | IntegerLittleEndian |  | FeeEnergy |
| 63 | 4 | IntegerLittleEndian |  | FeeCommand |
| 67 | 4 | IntegerLittleEndian |  | SpendableScalar |
| 71 | 4 | IntegerLittleEndian |  | SpendableStrength |
| 75 | 4 | IntegerLittleEndian |  | SpendableAgility |
| 79 | 4 | IntegerLittleEndian |  | SpendableVitality |
| 83 | 4 | IntegerLittleEndian |  | SpendableEnergy |
| 87 | 4 | IntegerLittleEndian |  | SpendableCommand |
| 91 | 4 | IntegerLittleEndian |  | ClaimedScalar |
| 95 | 4 | IntegerLittleEndian |  | ClaimedStrength |
| 99 | 4 | IntegerLittleEndian |  | ClaimedAgility |
| 103 | 4 | IntegerLittleEndian |  | ClaimedVitality |
| 107 | 4 | IntegerLittleEndian |  | ClaimedEnergy |
| 111 | 4 | IntegerLittleEndian |  | ClaimedCommand |
| 115 | 4 | IntegerLittleEndian |  | RemainingScalar |
| 119 | 4 | IntegerLittleEndian |  | RemainingStrength |
| 123 | 4 | IntegerLittleEndian |  | RemainingAgility |
| 127 | 4 | IntegerLittleEndian |  | RemainingVitality |
| 131 | 4 | IntegerLittleEndian |  | RemainingEnergy |
| 135 | 4 | IntegerLittleEndian |  | RemainingCommand |
| 139 | 4 | IntegerLittleEndian |  | ServerTime |

### AuctionOperationType Enum

Auction House mutation kinds.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Create | Create. |
| 1 | Bid | Bid. |
| 2 | Buyout | Buyout. |
| 3 | Cancel | Cancel. |
| 4 | Collect | Collect. |

### AuctionResult Enum

Auction House result codes.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Success | Success. |
| 1 | Disabled | Disabled. |
| 2 | NotAuthenticated | NotAuthenticated. |
| 3 | StateRestricted | StateRestricted. |
| 4 | LevelTooLow | LevelTooLow. |
| 5 | AccountTooNew | AccountTooNew. |
| 6 | AccountRestricted | AccountRestricted. |
| 7 | RateLimited | RateLimited. |
| 8 | InvalidRequest | InvalidRequest. |
| 9 | UnsupportedCurrency | UnsupportedCurrency. |
| 10 | CurrencyConfigurationInvalid | CurrencyConfigurationInvalid. |
| 11 | InvalidDuration | InvalidDuration. |
| 12 | InvalidPrice | InvalidPrice. |
| 13 | InvalidFruitBasket | InvalidFruitBasket. |
| 14 | ItemNotFound | ItemNotFound. |
| 15 | ItemNotEligible | ItemNotEligible. |
| 16 | ListingLimitReached | ListingLimitReached. |
| 17 | InsufficientCurrency | InsufficientCurrency. |
| 18 | ListingNotFound | ListingNotFound. |
| 19 | ListingNotActive | ListingNotActive. |
| 20 | ListingExpired | ListingExpired. |
| 21 | StaleListing | StaleListing. |
| 22 | SelfTrade | SelfTrade. |
| 23 | AlreadyHighestBidder | AlreadyHighestBidder. |
| 24 | BidTooLow | BidTooLow. |
| 25 | BuyoutUnavailable | BuyoutUnavailable. |
| 26 | CannotCancelWithBid | CannotCancelWithBid. |
| 27 | CollectionNotFound | CollectionNotFound. |
| 28 | InvalidClaimQuantity | InvalidClaimQuantity. |
| 29 | InventoryFull | InventoryFull. |
| 30 | MoneyLimit | MoneyLimit. |
| 31 | AlreadyProcessed | AlreadyProcessed. |
| 32 | ServiceUnavailable | ServiceUnavailable. |

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
| 9 | Mixed | A basket of several currencies, or a buy-now price. |