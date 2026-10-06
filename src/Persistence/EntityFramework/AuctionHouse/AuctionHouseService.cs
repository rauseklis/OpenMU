// <copyright file="AuctionHouseService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

using System.Threading;

/// <summary>
/// The Auction House operations which only read: opening the window, browsing, and viewing one listing. They never lock
/// a player or change a row, so a browse cannot force a save or delay a purchase.
/// </summary>
public sealed class AuctionHouseService
{
    private readonly AuctionHouseRepository _repository;
    private readonly Func<EntityDataContext> _contextFactory;
    private readonly AuctionHouseOptions _options;
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionHouseService"/> class.
    /// </summary>
    /// <param name="repository">The repository which runs the queries.</param>
    /// <param name="contextFactory">Creates a context for each request.</param>
    /// <param name="options">The configured options.</param>
    /// <param name="clock">The clock; the database time is used when this is not given.</param>
    public AuctionHouseService(AuctionHouseRepository repository, Func<EntityDataContext> contextFactory, AuctionHouseOptions options, Func<DateTimeOffset>? clock = null)
    {
        this._repository = repository;
        this._contextFactory = contextFactory;
        this._options = options;
        this._clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Opens the window: reports whether the feature is enabled, the server time, and the account's pending Mailbox count.
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

        await using var context = this._contextFactory();
        var pending = await this._repository.CountPendingCollectionsAsync(context, accountId, cancellationToken);
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
            await using var context = this._contextFactory();
            var page = await this._repository.SearchListingsAsync(context, search with { PageSize = this._options.PageSize }, cancellationToken);
            return new AuctionBrowseResult(AuctionResultCode.Success, page, now);
        }
        catch (ArgumentException)
        {
            return new AuctionBrowseResult(AuctionResultCode.InvalidRequest, default, now);
        }
    }

    /// <summary>
    /// Reads one listing, including its note and item snapshot.
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

        await using var context = this._contextFactory();
        var listing = await this._repository.GetListingAsync(context, listingId, cancellationToken);
        return listing is null
            ? new AuctionDetailResult(AuctionResultCode.ListingNotFound, null, now)
            : new AuctionDetailResult(AuctionResultCode.Success, listing, now);
    }
}
