# C2 D5 80 - AuctionOpenResponse (by server)

## Is sent when

Open response with currency catalog.

## Causes the following actions on the client side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x80  | Packet header - sub packet type identifier |
| 5 | 4 | IntegerLittleEndian |  | RequestId |
| 9 | 1 | AuctionResult |  | Result |
| 10 | 4 | IntegerLittleEndian |  | ServerTime |
| 14 | 4 | IntegerLittleEndian |  | ConfigurationVersion |
| 18 | 2 | ShortLittleEndian |  | ListingFeeBasisPoints |
| 20 | 2 | ShortLittleEndian |  | SuccessFeeBasisPoints |
| 22 | 1 | Byte |  | DurationMask |
| 23 | 1 | Byte |  | DefaultDurationHours |
| 24 | 1 | Byte |  | PageSize |
| 25 | 2 | ShortLittleEndian |  | PendingMailboxCount |
| 27 | 1 | Byte |  | CurrencyCount |
| 28 | AuctionCurrencyDescriptor.Length * CurrencyCount | Array of AuctionCurrencyDescriptor |  | Currencies |

### AuctionCurrencyDescriptor Structure



Length: 45 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 | AuctionCurrencyMode |  | CurrencyMode |
| 1 | 1 | Boolean |  | Enabled |
| 2 | 4 | IntegerLittleEndian |  | MinimumStartingPrice |
| 6 | 4 | IntegerLittleEndian |  | MaximumStartingPrice |
| 10 | 4 | IntegerLittleEndian |  | MinimumIncrement |
| 14 | 1 | Byte |  | IncrementPercent |
| 15 | 4 | IntegerLittleEndian |  | SpendableScalar |
| 19 | 4 | IntegerLittleEndian |  | SpendableStrength |
| 23 | 4 | IntegerLittleEndian |  | SpendableAgility |
| 27 | 4 | IntegerLittleEndian |  | SpendableVitality |
| 31 | 4 | IntegerLittleEndian |  | SpendableEnergy |
| 35 | 4 | IntegerLittleEndian |  | SpendableCommand |
| 39 | 2 | ShortLittleEndian |  | SmallPackUnits |
| 41 | 2 | ShortLittleEndian |  | MediumPackUnits |
| 43 | 2 | ShortLittleEndian |  | LargePackUnits |

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