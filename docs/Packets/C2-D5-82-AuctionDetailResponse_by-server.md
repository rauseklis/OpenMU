# C2 D5 82 - AuctionDetailResponse (by server)

## Is sent when

Listing detail.

## Causes the following actions on the client side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |   204   | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x82  | Packet header - sub packet type identifier |
| 5 | 4 | IntegerLittleEndian |  | RequestId |
| 9 | 1 | AuctionResult |  | Result |
| 10 | 4 | IntegerLittleEndian |  | ServerTime |
| 14 | 8 | LongLittleEndian |  | ListingId |
| 22 | 4 | IntegerLittleEndian |  | Version |
| 26 | 1 | AuctionListingStatus |  | Status |
| 27 | 1 | AuctionCurrencyMode |  | CurrencyMode |
| 28 | 1 | AuctionCategory |  | Category |
| 29 | 4 | IntegerLittleEndian |  | CurrentPriceScalar |
| 33 | 4 | IntegerLittleEndian |  | CurrentPriceStrength |
| 37 | 4 | IntegerLittleEndian |  | CurrentPriceAgility |
| 41 | 4 | IntegerLittleEndian |  | CurrentPriceVitality |
| 45 | 4 | IntegerLittleEndian |  | CurrentPriceEnergy |
| 49 | 4 | IntegerLittleEndian |  | CurrentPriceCommand |
| 53 | 4 | IntegerLittleEndian |  | BuyoutPriceScalar |
| 57 | 4 | IntegerLittleEndian |  | BuyoutPriceStrength |
| 61 | 4 | IntegerLittleEndian |  | BuyoutPriceAgility |
| 65 | 4 | IntegerLittleEndian |  | BuyoutPriceVitality |
| 69 | 4 | IntegerLittleEndian |  | BuyoutPriceEnergy |
| 73 | 4 | IntegerLittleEndian |  | BuyoutPriceCommand |
| 77 | 2 | ShortLittleEndian |  | BidCount |
| 79 | 4 | IntegerLittleEndian |  | OriginalEndsAt |
| 83 | 4 | IntegerLittleEndian |  | EndsAt |
| 87 | 10 | Binary |  | SellerName |
| 97 | 10 | Binary |  | CurrentBidderName |
| 107 | 1 | Byte |  | NoteLength |
| 108 | 80 | Binary |  | Note |
| 188 | 1 | Byte |  | ItemDataLength |
| 189 | 15 | Binary |  | ItemData |

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