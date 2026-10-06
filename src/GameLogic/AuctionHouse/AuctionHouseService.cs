// <copyright file="AuctionHouseService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Threading;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// The Auction House operations which only read: opening the window, browsing, and viewing one listing. They never lock
/// a player or change a row, so a browse cannot force a save or delay a purchase.
/// </summary>
public sealed class AuctionHouseService
{
    private readonly IAuctionHouseRepository _repository;
    private readonly AuctionHouseOptions _options;
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionHouseService"/> class.
    /// </summary>
    /// <param name="repository">The repository which runs the queries.</param>
    /// <param name="options">The configured options.</param>
    /// <param name="clock">The clock. The current UTC time is used when this is not given.</param>
    public AuctionHouseService(IAuctionHouseRepository repository, AuctionHouseOptions options, Func<DateTimeOffset>? clock = null)
    {
        this._repository = repository;
        this._options = options;
        this._clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Opens the window: reports the server time, the page size, and the account's pending Mailbox count.
    /// </summary>
    /// <param name="accountId">The account which opens the window.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The open result.</returns>
    public async Task<AuctionOpenResult> OpenAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var now = this._clock();
        if (!this._options.Enabled)
        {
            return new AuctionOpenResult(AuctionResultCode.Disabled, 0, now, this._options.PageSize);
        }

        var pending = await this._repository.CountPendingCollectionsAsync(accountId, cancellationToken);
        return new AuctionOpenResult(AuctionResultCode.Success, pending, now, this._options.PageSize);
    }

    /// <summary>
    /// Browses active listings of one currency, bounded to the configured page size.
    /// </summary>
    /// <param name="search">The browse request. Its page size is replaced by the configured one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The browse result.</returns>
    public async Task<AuctionBrowseResult> BrowseAsync(AuctionListingSearch search, CancellationToken cancellationToken = default)
    {
        var now = this._clock();
        if (!this._options.Enabled)
        {
            return new AuctionBrowseResult(AuctionResultCode.Disabled, default, now);
        }

        if (search.Page < 1)
        {
            return new AuctionBrowseResult(AuctionResultCode.InvalidRequest, default, now);
        }

        try
        {
            var page = await this._repository.SearchListingsAsync(search with { PageSize = this._options.PageSize }, cancellationToken);
            return new AuctionBrowseResult(AuctionResultCode.Success, page, now);
        }
        catch (ArgumentException)
        {
            return new AuctionBrowseResult(AuctionResultCode.InvalidRequest, default, now);
        }
    }

    /// <summary>
    /// Lists one inventory item of the character for sale. The item and its eligibility are checked inside the transaction,
    /// so the same check decides both a first request and a retry.
    /// </summary>
    /// <param name="request">The listing request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The listing result.</returns>
    public async Task<AuctionListItemResult> ListItemAsync(AuctionListItemRequest request, CancellationToken cancellationToken = default)
    {
        var now = this._clock();
        if (!this._options.Enabled)
        {
            return new AuctionListItemResult(AuctionResultCode.Disabled, Guid.Empty, 0, now);
        }

        // Zen, the seven jewel modes and the fruit basket can list.
        var currency = (AuctionCurrencyMode)request.Currency;
        if (currency is not (AuctionCurrencyMode.Zen or AuctionCurrencyMode.Chaos or AuctionCurrencyMode.Bless or AuctionCurrencyMode.Soul
            or AuctionCurrencyMode.Life or AuctionCurrencyMode.Creation or AuctionCurrencyMode.Guardian or AuctionCurrencyMode.Harmony or AuctionCurrencyMode.Fruits))
        {
            return new AuctionListItemResult(AuctionResultCode.UnsupportedCurrency, Guid.Empty, 0, now);
        }

        if (!AuctionRules.IsValidDuration(request.DurationHours))
        {
            return new AuctionListItemResult(AuctionResultCode.InvalidDuration, Guid.Empty, 0, now);
        }

        if (currency != AuctionCurrencyMode.Fruits && (!AuctionRules.IsPriceInRange(currency, request.StartingPrice)
            || request.BuyoutPrice is { } buyout && (buyout < request.StartingPrice || !AuctionRules.IsPriceInRange(currency, buyout))))
        {
            return new AuctionListItemResult(AuctionResultCode.InvalidPrice, Guid.Empty, 0, now);
        }

        var isFruits = currency == AuctionCurrencyMode.Fruits;
        if (isFruits && (request.StartingFruits is not { } basket || !FruitRules.IsValidStartingBasket(basket) || request.BuyoutPrice is not null
            || request.BuyoutFruits is { } buyoutBasket && !FruitRules.IsValidBuyout(basket, buyoutBasket)))
        {
            return new AuctionListItemResult(AuctionResultCode.InvalidFruitBasket, Guid.Empty, 0, now);
        }

        var feeFruits = isFruits ? FruitRules.CalculateFees(request.StartingFruits!.Value, this._options.ListingFeeBasisPoints) : (FruitBasket?)null;
        var draft = new ItemListingDraft(
            request.OperationId,
            request.AccountId,
            request.CharacterId,
            request.SellerName,
            request.SellerIp,
            request.InventorySlot,
            request.Currency,
            request.Category,
            isFruits ? 0 : request.StartingPrice,
            request.BuyoutPrice,
            now,
            now.AddHours(request.DurationHours),
            isFruits ? 0 : AuctionRules.CalculateFee(request.StartingPrice, this._options.ListingFeeBasisPoints),
            this._options.MaximumActiveListingsPerAccount,
            this._options.MaximumActiveListingsPerCharacter,
            request.StartingFruits,
            feeFruits,
            isFruits ? request.BuyoutFruits : null);
        var result = await this._repository.ListItemAsync(draft, IsEligible, cancellationToken);
        AuctionHouseMetrics.RecordOperation("create", result.Outcome.ToString());
        if (result.Outcome == AuctionItemListingOutcome.Created)
        {
            AuctionHouseMetrics.RecordListingFee(currency, draft.ListingFee, feeFruits);
        }

        return new AuctionListItemResult(MapListingOutcome(result.Outcome), result.ListingId, result.ListingNumber, now);
    }

    private static bool IsEligible(ItemListingFacts facts)
    {
        var candidate = new ListingCandidate(
            facts.IsTradable,
            facts.IsPersonalStoreSellable,
            facts.IsBoundToCharacter,
            facts.IsQuestItem,
            facts.Durability,
            facts.InventorySlot >= InventoryConstants.EquippableSlotsCount ? ItemLocation.MainInventory : ItemLocation.Equipped,
            facts.IsAlreadyReferenced);
        return ListingEligibility.Evaluate(candidate) == ListingEligibilityResult.Eligible;
    }

    private static AuctionResultCode MapListingOutcome(AuctionItemListingOutcome outcome)
        => outcome switch
        {
            AuctionItemListingOutcome.Created or AuctionItemListingOutcome.Replayed => AuctionResultCode.Success,
            AuctionItemListingOutcome.ItemNotFound => AuctionResultCode.ItemNotFound,
            AuctionItemListingOutcome.NotEligible or AuctionItemListingOutcome.ItemAlreadyListed => AuctionResultCode.ItemNotEligible,
            AuctionItemListingOutcome.ListingLimitReached => AuctionResultCode.ListingLimitReached,
            AuctionItemListingOutcome.InsufficientCurrency => AuctionResultCode.InsufficientCurrency,
            AuctionItemListingOutcome.CharacterNotOwned => AuctionResultCode.InvalidRequest,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

    /// <summary>
    /// Places a bid. The minimum bid and the anti-sniping extension are computed from the listing as it was read, and the
    /// repository checks them again against the locked listing and its version.
    /// </summary>
    /// <param name="request">The bid request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bid result.</returns>
    public async Task<AuctionMutationResult> BidAsync(AuctionBidRequest request, CancellationToken cancellationToken = default)
    {
        var now = this._clock();
        if (!this._options.Enabled)
        {
            return new AuctionMutationResult(AuctionResultCode.Disabled, now);
        }

        var listing = await this._repository.GetListingAsync(request.ListingId, cancellationToken);
        if (listing is null)
        {
            return new AuctionMutationResult(AuctionResultCode.ListingNotFound, now);
        }

        if (listing.Currency == (byte)AuctionCurrencyMode.Fruits)
        {
            if (request.BidFruits is not { } bidBasket)
            {
                return new AuctionMutationResult(AuctionResultCode.InvalidFruitBasket, now);
            }

            var fruitDraft = new FruitBidDraft(
                request.OperationId,
                request.AccountId,
                request.CharacterId,
                request.ListingId,
                request.ExpectedVersion,
                bidBasket,
                request.BidderIp,
                now,
                AuctionRules.ApplyAntiSniping(listing.EndsAt, listing.OriginalEndsAt, now));
            var fruitResult = await this._repository.PlaceFruitBidAsync(fruitDraft, cancellationToken).ConfigureAwait(false);
            AuctionHouseMetrics.RecordOperation("bid", fruitResult.Outcome.ToString());
            return new AuctionMutationResult(MapBidOutcome(fruitResult.Outcome), now);
        }

        var draft = new ScalarBidDraft(
            request.OperationId,
            request.AccountId,
            request.CharacterId,
            request.ListingId,
            request.ExpectedVersion,
            request.BidAmount,
            AuctionRules.GetMinimumBid(AuctionCurrencyMode.Zen, listing.StartingPrice, listing.CurrentBid ?? 0),
            request.BidderIp,
            now,
            AuctionRules.ApplyAntiSniping(listing.EndsAt, listing.OriginalEndsAt, now));
        var result = await this._repository.PlaceScalarBidAsync(draft, cancellationToken);
        AuctionHouseMetrics.RecordOperation("bid", result.Outcome.ToString());
        return new AuctionMutationResult(MapBidOutcome(result.Outcome), now);
    }

    /// <summary>
    /// Buys a listing out at its buyout price. The success fee is computed from the buyout price.
    /// </summary>
    /// <param name="request">The buyout request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The buyout result.</returns>
    public async Task<AuctionMutationResult> BuyoutAsync(AuctionBuyoutRequest request, CancellationToken cancellationToken = default)
    {
        var now = this._clock();
        if (!this._options.Enabled)
        {
            return new AuctionMutationResult(AuctionResultCode.Disabled, now);
        }

        var listing = await this._repository.GetListingAsync(request.ListingId, cancellationToken);
        if (listing is null)
        {
            return new AuctionMutationResult(AuctionResultCode.ListingNotFound, now);
        }

        if (listing.Currency == (byte)AuctionCurrencyMode.Fruits)
        {
            var fruitBuyout = new FruitBuyoutDraft(request.OperationId, request.AccountId, request.CharacterId, request.ListingId, request.ExpectedVersion, request.BuyerIp, now, this._options.SuccessFeeBasisPoints);
            var fruitResult = await this._repository.PlaceFruitBuyoutAsync(fruitBuyout, cancellationToken).ConfigureAwait(false);
            AuctionHouseMetrics.RecordOperation("buyout", fruitResult.Outcome.ToString());
            return new AuctionMutationResult(MapBuyoutOutcome(fruitResult.Outcome), now);
        }

        if (listing.BuyoutPrice is not { } buyoutPrice)
        {
            return new AuctionMutationResult(AuctionResultCode.BuyoutUnavailable, now);
        }

        var draft = new BuyoutDraft(
            request.OperationId,
            request.AccountId,
            request.CharacterId,
            request.ListingId,
            request.ExpectedVersion,
            AuctionRules.CalculateFee(buyoutPrice, this._options.SuccessFeeBasisPoints),
            request.BuyerIp,
            now);
        var result = await this._repository.BuyoutAsync(draft, cancellationToken);
        AuctionHouseMetrics.RecordOperation("buyout", result.Outcome.ToString());
        return new AuctionMutationResult(MapBuyoutOutcome(result.Outcome), now);
    }

    private static AuctionResultCode MapBidOutcome(AuctionBidOutcome outcome)
        => outcome switch
        {
            AuctionBidOutcome.Placed or AuctionBidOutcome.Replayed => AuctionResultCode.Success,
            AuctionBidOutcome.ListingNotFound => AuctionResultCode.ListingNotFound,
            AuctionBidOutcome.StaleListing => AuctionResultCode.StaleListing,
            AuctionBidOutcome.NotActive => AuctionResultCode.ListingNotActive,
            AuctionBidOutcome.SelfBid => AuctionResultCode.SelfTrade,
            AuctionBidOutcome.BidTooLow => AuctionResultCode.BidTooLow,
            AuctionBidOutcome.InsufficientCurrency => AuctionResultCode.InsufficientCurrency,
            AuctionBidOutcome.UnsupportedCurrency => AuctionResultCode.UnsupportedCurrency,
            AuctionBidOutcome.CharacterNotOwned => AuctionResultCode.InvalidRequest,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

    private static AuctionResultCode MapBuyoutOutcome(AuctionBuyoutOutcome outcome)
        => outcome switch
        {
            AuctionBuyoutOutcome.Bought or AuctionBuyoutOutcome.Replayed => AuctionResultCode.Success,
            AuctionBuyoutOutcome.ListingNotFound => AuctionResultCode.ListingNotFound,
            AuctionBuyoutOutcome.StaleListing => AuctionResultCode.StaleListing,
            AuctionBuyoutOutcome.NotActive => AuctionResultCode.ListingNotActive,
            AuctionBuyoutOutcome.SelfBuy => AuctionResultCode.SelfTrade,
            AuctionBuyoutOutcome.BuyoutUnavailable => AuctionResultCode.BuyoutUnavailable,
            AuctionBuyoutOutcome.InsufficientCurrency => AuctionResultCode.InsufficientCurrency,
            AuctionBuyoutOutcome.UnsupportedCurrency => AuctionResultCode.UnsupportedCurrency,
            AuctionBuyoutOutcome.CharacterNotOwned => AuctionResultCode.InvalidRequest,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

    /// <summary>
    /// Cancels a listing which has no bid. The listing fee is not refunded.
    /// </summary>
    /// <param name="request">The cancellation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation result.</returns>
    public async Task<AuctionMutationResult> CancelAsync(AuctionCancelRequest request, CancellationToken cancellationToken = default)
    {
        var now = this._clock();
        if (!this._options.Enabled)
        {
            return new AuctionMutationResult(AuctionResultCode.Disabled, now);
        }

        var draft = new CancelDraft(request.OperationId, request.AccountId, request.ListingId, request.ExpectedVersion, now);
        var result = await this._repository.CancelAsync(draft, cancellationToken);
        AuctionHouseMetrics.RecordOperation("cancel", result.Outcome.ToString());
        return new AuctionMutationResult(MapCancelOutcome(result.Outcome), now);
    }

    /// <summary>
    /// Collects a pending collection. A collection of another account is reported as not found, not as forbidden.
    /// </summary>
    /// <param name="request">The collection request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The collection result.</returns>
    public async Task<AuctionCollectResult> CollectAsync(AuctionCollectRequest request, CancellationToken cancellationToken = default)
    {
        var now = this._clock();
        if (!this._options.Enabled)
        {
            return new AuctionCollectResult(AuctionResultCode.Disabled, 0, null, 0, now);
        }

        var draft = new ClaimDraft(
            request.OperationId,
            request.AccountId,
            request.CharacterId,
            request.CollectionId,
            request.ExpectedVersion,
            now,
            request.RequestedScalar,
            request.RequestedFruits);
        var result = await this._repository.ClaimAsync(draft, cancellationToken);
        AuctionHouseMetrics.RecordOperation("collect", result.Outcome.ToString());
        return new AuctionCollectResult(MapClaimOutcome(result.Outcome), result.Kind, result.ItemId, result.Amount, now);
    }

    private static AuctionResultCode MapCancelOutcome(AuctionCancelOutcome outcome)
        => outcome switch
        {
            AuctionCancelOutcome.Cancelled or AuctionCancelOutcome.Replayed => AuctionResultCode.Success,

            // A seller who does not own the listing learns nothing more than an unknown listing.
            AuctionCancelOutcome.ListingNotFound or AuctionCancelOutcome.NotSeller => AuctionResultCode.ListingNotFound,
            AuctionCancelOutcome.StaleListing => AuctionResultCode.StaleListing,
            AuctionCancelOutcome.NotActive => AuctionResultCode.ListingNotActive,
            AuctionCancelOutcome.CannotCancelWithBid => AuctionResultCode.CannotCancelWithBid,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

    private static AuctionResultCode MapClaimOutcome(AuctionClaimOutcome outcome)
        => outcome switch
        {
            AuctionClaimOutcome.Claimed or AuctionClaimOutcome.Replayed => AuctionResultCode.Success,
            AuctionClaimOutcome.NotFound => AuctionResultCode.CollectionNotFound,
            AuctionClaimOutcome.Stale => AuctionResultCode.StaleListing,
            AuctionClaimOutcome.AlreadyClaimed => AuctionResultCode.AlreadyProcessed,
            AuctionClaimOutcome.MoneyLimit => AuctionResultCode.MoneyLimit,
            AuctionClaimOutcome.InvalidClaimQuantity => AuctionResultCode.InvalidClaimQuantity,
            AuctionClaimOutcome.InventoryFull => AuctionResultCode.InventoryFull,
            AuctionClaimOutcome.UnsupportedCurrency => AuctionResultCode.UnsupportedCurrency,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

    /// <summary>
    /// Finalizes one batch of listings whose expiry has passed. Listings another worker holds are skipped, and each listing
    /// is finalized at most once.
    /// </summary>
    /// <param name="batchSize">The maximum number of listings in this batch.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of listings finalized.</returns>
    public async Task<int> FinalizeDueListingsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (!this._options.Enabled)
        {
            return 0;
        }

        var finalized = await this._repository.ExpireBatchAsync(this._clock(), batchSize, price => AuctionRules.CalculateFee(price, this._options.SuccessFeeBasisPoints), cancellationToken);
        AuctionHouseMetrics.RecordExpiries(finalized);
        return finalized;
    }

    /// <summary>
    /// Lists the listings of the account, newest first.
    /// </summary>
    /// <param name="accountId">The seller's account.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="status">The status to filter by, or <c>null</c> for every status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page of listings.</returns>
    public async Task<AuctionBrowseResult> MyListingsAsync(Guid accountId, int page, byte? status, CancellationToken cancellationToken = default)
    {
        var now = this._clock();
        if (!this._options.Enabled)
        {
            return new AuctionBrowseResult(AuctionResultCode.Disabled, default, now);
        }

        if (page < 1)
        {
            return new AuctionBrowseResult(AuctionResultCode.InvalidRequest, default, now);
        }

        var listings = await this._repository.ListSellerListingsAsync(accountId, page, this._options.PageSize, status, cancellationToken);
        return new AuctionBrowseResult(AuctionResultCode.Success, listings, now);
    }

    /// <summary>
    /// Lists the mailbox entries of the account, newest first.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="kind">The kind to filter by, or <c>null</c> for every kind.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mailbox result.</returns>
    public async Task<AuctionMailboxResult> MailboxAsync(Guid accountId, int page, byte? kind, CancellationToken cancellationToken = default)
    {
        var now = this._clock();
        if (!this._options.Enabled)
        {
            return new AuctionMailboxResult(AuctionResultCode.Disabled, default, now);
        }

        if (page < 1)
        {
            return new AuctionMailboxResult(AuctionResultCode.InvalidRequest, default, now);
        }

        var entries = await this._repository.ListCollectionsAsync(accountId, page, this._options.PageSize, kind, cancellationToken);
        return new AuctionMailboxResult(AuctionResultCode.Success, entries, now);
    }

    /// <summary>
    /// Checks the stored data against the Auction House invariants. Reporting only; it runs even while the feature is disabled,
    /// so operators can always inspect the data.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The broken invariants.</returns>
    public async Task<IReadOnlyList<AuctionReconciliationFinding>> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var findings = await this._repository.ReconcileAsync(cancellationToken);
        AuctionHouseMetrics.RecordReconciliationFindings(findings);
        return findings;
    }

    /// <summary>
    /// Claims one batch of undelivered notifications and gives each to the delivery callback. The claim marks them delivered,
    /// so a notification whose player is offline is not retried: the Mailbox still shows the outcome. A notification whose
    /// delivery throws is counted and does not stop the batch.
    /// </summary>
    /// <param name="batchSize">The maximum number of notifications to claim.</param>
    /// <param name="deliver">Shows one notification to its account, or reports that the account is offline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The totals of the batch.</returns>
    public async Task<AuctionNotificationDeliveryResult> DeliverNotificationsAsync(int batchSize, Func<AuctionNotificationRow, Task<AuctionNotificationDeliveryOutcome>> deliver, CancellationToken cancellationToken = default)
    {
        if (!this._options.Enabled)
        {
            return default;
        }

        var claimed = await this._repository.ClaimUndeliveredNotificationsAsync(batchSize, this._clock(), cancellationToken).ConfigureAwait(false);
        var delivered = 0;
        var notOnline = 0;
        var failed = 0;
        foreach (var notification in claimed)
        {
            try
            {
                var outcome = await deliver(notification).ConfigureAwait(false);
                if (outcome == AuctionNotificationDeliveryOutcome.Delivered)
                {
                    delivered++;
                }
                else
                {
                    notOnline++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
            }
        }

        AuctionHouseMetrics.RecordNotifications(delivered, notOnline, failed);
        return new AuctionNotificationDeliveryResult(delivered, notOnline, failed);
    }

    /// <summary>
    /// Tells whether the account has an active Auction House restriction.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when the account is restricted.</returns>
    public Task<bool> IsRestrictedAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken = default)
        => this._repository.IsAccountRestrictedAsync(accountId, now, cancellationToken);

    /// <summary>
    /// Loads the items of listings, so the browse rows can carry them.
    /// </summary>
    /// <param name="itemIds">The items.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The items by identifier; empty when the feature is disabled.</returns>
    public async Task<IReadOnlyDictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item>> LoadItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken = default)
    {
        return this._options.Enabled
            ? await this._repository.GetItemsAsync(itemIds, cancellationToken)
            : new Dictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item>();
    }

    /// <summary>
    /// Resolves a listing number from the wire to the listing identifier.
    /// </summary>
    /// <param name="listingNumber">The listing number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The listing identifier, or <c>null</c> when the feature is disabled or the number is unknown.</returns>
    public async Task<Guid?> ResolveListingIdAsync(long listingNumber, CancellationToken cancellationToken = default)
    {
        return this._options.Enabled ? await this._repository.FindListingIdAsync(listingNumber, cancellationToken) : null;
    }

    /// <summary>
    /// Resolves a collection number from the wire to the collection identifier.
    /// </summary>
    /// <param name="collectionNumber">The collection number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The collection identifier, or <c>null</c> when the feature is disabled or the number is unknown.</returns>
    public async Task<Guid?> ResolveCollectionIdAsync(long collectionNumber, CancellationToken cancellationToken = default)
    {
        return this._options.Enabled ? await this._repository.FindCollectionIdAsync(collectionNumber, cancellationToken) : null;
    }

    /// <summary>
    /// Reads one listing, including its note.
    /// </summary>
    /// <param name="listingId">The listing to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The detail result.</returns>
    public async Task<AuctionDetailResult> DetailAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var now = this._clock();
        if (!this._options.Enabled)
        {
            return new AuctionDetailResult(AuctionResultCode.Disabled, null, now);
        }

        var listing = await this._repository.GetListingAsync(listingId, cancellationToken);
        return listing is null
            ? new AuctionDetailResult(AuctionResultCode.ListingNotFound, null, now)
            : new AuctionDetailResult(AuctionResultCode.Success, listing, now);
    }
}
