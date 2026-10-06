// <copyright file="IAuctionHouseRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

using System.Threading;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// The transactional Auction House operations. Each operation runs in one database transaction and writes its operation
/// record with it, so a retried operation returns its stored outcome instead of happening twice.
/// </summary>
public interface IAuctionHouseRepository
{
    /// <summary>
    /// Creates a listing together with its operation record.
    /// </summary>
    /// <param name="draft">The new listing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    Task<AuctionCreateResult> CreateListingAsync(AuctionListingDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places a scalar bid, escrowing it and releasing the displaced bid as a refund.
    /// </summary>
    /// <param name="draft">The bid.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    Task<AuctionBidResult> PlaceScalarBidAsync(ScalarBidDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places a fruit-basket bid, escrowing its fruits and releasing the displaced bid as a refund.
    /// </summary>
    /// <param name="draft">The bid.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    Task<AuctionBidResult> PlaceFruitBidAsync(FruitBidDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Buys a listing out at its buyout price.
    /// </summary>
    /// <param name="draft">The buyout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    Task<AuctionBuyoutResult> BuyoutAsync(BuyoutDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a listing which has no bid.
    /// </summary>
    /// <param name="draft">The cancellation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    Task<AuctionCancelResult> CancelAsync(CancelDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalizes one listing if its expiry has passed.
    /// </summary>
    /// <param name="draft">The expiry check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the check.</returns>
    Task<AuctionExpireResult> ExpireAsync(ExpireDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalizes a batch of due listings, skipping listings which another worker holds.
    /// </summary>
    /// <param name="now">The current time, in UTC.</param>
    /// <param name="batchSize">The maximum number of listings in this batch.</param>
    /// <param name="successFee">The success fee for a sale price.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of listings finalized.</returns>
    Task<int> ExpireBatchAsync(DateTimeOffset now, int batchSize, Func<long, long> successFee, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims a collection, fully or partially.
    /// </summary>
    /// <param name="draft">The claim.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    Task<AuctionClaimResult> ClaimAsync(ClaimDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Browses the active listings of one currency.
    /// </summary>
    /// <param name="search">The browse request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One page of listings.</returns>
    Task<AuctionListingPage> SearchListingsAsync(AuctionListingSearch search, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one listing without locking it.
    /// </summary>
    /// <param name="listingId">The listing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The listing, or <c>null</c> when it does not exist.</returns>
    Task<AuctionListingDetail?> GetListingAsync(Guid listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the collections of an account which are not fully claimed.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of pending collections.</returns>
    Task<int> CountPendingCollectionsAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the listings of one seller, newest first.
    /// </summary>
    /// <param name="accountId">The seller's account.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">The number of listings per page.</param>
    /// <param name="status">The status to filter by, or <c>null</c> for every status.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One page of listings.</returns>
    Task<AuctionListingPage> ListSellerListingsAsync(Guid accountId, int page, int pageSize, byte? status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the collections of one account, newest first.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="kind">The kind to filter by, or <c>null</c> for every kind.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One page of mailbox entries.</returns>
    Task<AuctionCollectionPage> ListCollectionsAsync(Guid accountId, int page, int pageSize, byte? kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims undelivered notifications for delivery and marks them delivered. Claimed rows are locked with
    /// <c>SKIP LOCKED</c>, so two workers never claim the same notification.
    /// </summary>
    /// <param name="batchSize">The maximum number of notifications to claim.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The claimed notifications, oldest first.</returns>
    Task<IReadOnlyList<AuctionNotificationRow>> ClaimUndeliveredNotificationsAsync(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the stored Auction House data against its invariants. Read-only: it reports, and never repairs.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The broken invariants; empty when the data is consistent.</returns>
    Task<IReadOnlyList<AuctionReconciliationFinding>> ReconcileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads listed items with their definition and options, read-only.
    /// </summary>
    /// <param name="itemIds">The items.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The items by identifier.</returns>
    Task<IReadOnlyDictionary<Guid, Item>> GetItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the identifier of a listing from its wire number.
    /// </summary>
    /// <param name="listingNumber">The listing number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The listing identifier, or <c>null</c> when no listing has the number.</returns>
    Task<Guid?> FindListingIdAsync(long listingNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the identifier of a collection from its wire number.
    /// </summary>
    /// <param name="collectionNumber">The collection number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The collection identifier, or <c>null</c> when no collection has the number.</returns>
    Task<Guid?> FindCollectionIdAsync(long collectionNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists an inventory item. Inside the transaction the item is located by its slot, the eligibility callback decides
    /// whether it may be listed, the fee is charged, and the item leaves the inventory.
    /// </summary>
    /// <param name="draft">The listing values.</param>
    /// <param name="isEligible">Decides whether the item may be listed, from its facts as read inside the transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    Task<AuctionItemListingResult> ListItemAsync(ItemListingDraft draft, Func<ItemListingFacts, bool> isEligible, CancellationToken cancellationToken = default);
}
