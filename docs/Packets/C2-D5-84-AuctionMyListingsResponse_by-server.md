# C2 D5 84 - AuctionMyListingsResponse (by server)

## Is sent when

Owned listings page.

## Causes the following actions on the client side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x84  | Packet header - sub packet type identifier |
| 5 | 4 | IntegerLittleEndian |  | RequestId |
| 9 | 1 | AuctionResult |  | Result |
| 10 | 2 | ShortLittleEndian |  | Page |
| 12 | 2 | ShortLittleEndian |  | TotalPages |
| 14 | 4 | IntegerLittleEndian |  | TotalCount |
| 18 | 4 | IntegerLittleEndian |  | ServerTime |
| 22 | 1 | Byte |  | ListingCount |
| 23 | AuctionListingSummary.Length * ListingCount | Array of AuctionListingSummary |  | Listings |

### AuctionListingSummary Structure



Length: 95 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 8 | LongLittleEndian |  | ListingId |
| 8 | 4 | IntegerLittleEndian |  | Version |
| 12 | 1 | AuctionListingStatus |  | Status |
| 13 | 1 | AuctionCurrencyMode |  | CurrencyMode |
| 14 | 1 | AuctionCategory |  | Category |
| 15 | 4 | IntegerLittleEndian |  | CurrentPriceScalar |
| 19 | 4 | IntegerLittleEndian |  | CurrentPriceStrength |
| 23 | 4 | IntegerLittleEndian |  | CurrentPriceAgility |
| 27 | 4 | IntegerLittleEndian |  | CurrentPriceVitality |
| 31 | 4 | IntegerLittleEndian |  | CurrentPriceEnergy |
| 35 | 4 | IntegerLittleEndian |  | CurrentPriceCommand |
| 39 | 4 | IntegerLittleEndian |  | BuyoutPriceScalar |
| 43 | 4 | IntegerLittleEndian |  | BuyoutPriceStrength |
| 47 | 4 | IntegerLittleEndian |  | BuyoutPriceAgility |
| 51 | 4 | IntegerLittleEndian |  | BuyoutPriceVitality |
| 55 | 4 | IntegerLittleEndian |  | BuyoutPriceEnergy |
| 59 | 4 | IntegerLittleEndian |  | BuyoutPriceCommand |
| 63 | 2 | ShortLittleEndian |  | BidCount |
| 65 | 4 | IntegerLittleEndian |  | EndsAt |
| 69 | 10 | Binary |  | SellerName |
| 79 | 1 | Byte |  | ItemDataLength |
| 80 | 15 | Binary |  | ItemData |

### AuctionListingStatus Enum

Auction House listing states.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Active | Active. |
| 1 | Sold | Sold. |
| 2 | Expired | Expired. |
| 3 | Cancelled | Cancelled. |
| 4 | AdminRemoved | AdminRemoved. |

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

### AuctionCategory Enum

Auction House categories.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Weapon | Weapon. |
| 1 | Armor | Armor. |
| 2 | Wing | Wing. |
| 3 | PetHelper | PetHelper. |
| 4 | JewelMaterial | JewelMaterial. |
| 5 | Consumable | Consumable. |
| 6 | Miscellaneous | Miscellaneous. |
| 7 | ScrollOrb | ScrollOrb. |

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