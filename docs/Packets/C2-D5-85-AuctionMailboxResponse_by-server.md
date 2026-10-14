# C2 D5 85 - AuctionMailboxResponse (by server)

## Is sent when

Mailbox page.

## Causes the following actions on the client side

Auction House service, see docs/superpowers/specs/2026-10-06-auction-house-design.md.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xD5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x85  | Packet header - sub packet type identifier |
| 5 | 4 | IntegerLittleEndian |  | RequestId |
| 9 | 1 | AuctionResult |  | Result |
| 10 | 2 | ShortLittleEndian |  | Page |
| 12 | 2 | ShortLittleEndian |  | TotalPages |
| 14 | 4 | IntegerLittleEndian |  | TotalCount |
| 18 | 4 | IntegerLittleEndian |  | ServerTime |
| 22 | 1 | Byte |  | EntryCount |
| 23 | AuctionMailboxEntry.Length * EntryCount | Array of AuctionMailboxEntry |  | Entries |

### AuctionMailboxEntry Structure



Length: 192 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 8 | LongLittleEndian |  | CollectionId |
| 8 | 1 | AuctionCollectionKind |  | CollectionKind |
| 9 | 1 | AuctionCollectionStatus |  | CollectionStatus |
| 10 | 1 | AuctionCurrencyMode |  | CurrencyMode |
| 11 | 4 | IntegerLittleEndian |  | OriginalScalar |
| 15 | 4 | IntegerLittleEndian |  | OriginalStrength |
| 19 | 4 | IntegerLittleEndian |  | OriginalAgility |
| 23 | 4 | IntegerLittleEndian |  | OriginalVitality |
| 27 | 4 | IntegerLittleEndian |  | OriginalEnergy |
| 31 | 4 | IntegerLittleEndian |  | OriginalCommand |
| 35 | 4 | IntegerLittleEndian |  | RemainingScalar |
| 39 | 4 | IntegerLittleEndian |  | RemainingStrength |
| 43 | 4 | IntegerLittleEndian |  | RemainingAgility |
| 47 | 4 | IntegerLittleEndian |  | RemainingVitality |
| 51 | 4 | IntegerLittleEndian |  | RemainingEnergy |
| 55 | 4 | IntegerLittleEndian |  | RemainingCommand |
| 59 | 8 | LongLittleEndian |  | SourceListingId |
| 67 | 1 | Boolean |  | HasItem |
| 68 | 1 | Byte |  | ItemDataLength |
| 69 | 15 | Binary |  | ItemData |
| 84 | 4 | IntegerLittleEndian |  | Version |
| 88 | 52 | Binary |  | OriginalBasket |
| 140 | 52 | Binary |  | RemainingBasket |

### AuctionCollectionKind Enum

Auction House collection kinds.

| Value | Name | Description |
|-------|------|-------------|
| 0 | PurchasedItem | PurchasedItem. |
| 1 | ReturnedItem | ReturnedItem. |
| 2 | SaleProceeds | SaleProceeds. |
| 3 | OutbidRefund | OutbidRefund. |
| 4 | TenderChange | TenderChange. |
| 5 | AdminRefund | AdminRefund. |
| 6 | FeeRefund | The flat sale fee returned because the listing ended unsold. |

### AuctionCollectionStatus Enum

Auction House collection states.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Pending | Pending. |
| 1 | PartiallyClaimed | PartiallyClaimed. |
| 2 | Claimed | Claimed. |

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