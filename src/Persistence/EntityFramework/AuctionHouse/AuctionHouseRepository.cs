// <copyright file="AuctionHouseRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using Npgsql;

/// <summary>
/// Persists Auction House operations. Each operation runs in one database transaction, and its operation record is
/// written with it, so an operation either fully happens or not at all.
/// </summary>
public sealed class AuctionHouseRepository : IAuctionHouseRepository
{
    private const byte CreateKind = 0;
    private const int CreatedOutcome = 0;
    private const byte BidKind = 1;
    private const byte BuyoutKind = 2;
    private const byte CancelKind = 3;
    private const byte ClaimKind = 4;
    private const byte FeeRemovedKind = 5;
    private const byte OutbidNotificationKind = 0;
    private const byte SoldNotificationKind = 1;
    private const byte BoughtNotificationKind = 2;
    private const byte ExpiredNotificationKind = 3;
    private const byte ReturnedNotificationKind = 4;

    /// <summary>
    /// The default inventory money limit, used when no configured limit is given.
    /// </summary>
    public const int DefaultMoneyLimit = 2_000_000_000;

    private const string ExpirySavepoint = "expiry_listing";

    private readonly int _moneyLimit;
    private readonly Func<EntityDataContext> _contextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionHouseRepository"/> class.
    /// </summary>
    /// <param name="contextFactory">Creates a context for each operation of the interface. The default creates a new context.</param>
    /// <param name="moneyLimit">The maximum Zen an inventory may hold; the server's configured limit.</param>
    public AuctionHouseRepository(Func<EntityDataContext>? contextFactory = null, int moneyLimit = DefaultMoneyLimit)
    {
        this._contextFactory = contextFactory ?? (() => new EntityDataContext());
        this._moneyLimit = moneyLimit;
    }

    /// <inheritdoc />
    public Task<AuctionCreateResult> CreateListingAsync(AuctionListingDraft draft, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.CreateListingAsync(context, draft, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionBidResult> PlaceScalarBidAsync(ScalarBidDraft draft, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.PlaceScalarBidAsync(context, draft, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionBuyoutResult> BuyoutAsync(BuyoutDraft draft, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.BuyoutAsync(context, draft, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionCancelResult> CancelAsync(CancelDraft draft, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.CancelAsync(context, draft, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionExpireResult> ExpireAsync(ExpireDraft draft, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.ExpireAsync(context, draft, cancellationToken));

    /// <inheritdoc />
    public Task<int> ExpireBatchAsync(DateTimeOffset now, int batchSize, Func<long, long> successFee, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.ExpireBatchAsync(context, now, batchSize, successFee, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionClaimResult> ClaimAsync(ClaimDraft draft, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.ClaimAsync(context, draft, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionListingPage> SearchListingsAsync(AuctionListingSearch search, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.SearchListingsAsync(context, search, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionListingDetail?> GetListingAsync(Guid listingId, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.GetListingAsync(context, listingId, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionListingPage> ListSellerListingsAsync(Guid accountId, int page, int pageSize, byte? status, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.ListSellerListingsAsync(context, accountId, page, pageSize, status, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionCollectionPage> ListCollectionsAsync(Guid accountId, int page, int pageSize, byte? kind, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.ListCollectionsAsync(context, accountId, page, pageSize, kind, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<AuctionNotificationRow>> ClaimUndeliveredNotificationsAsync(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.ClaimUndeliveredNotificationsAsync(context, batchSize, now, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item>> GetItemsAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.GetItemsAsync(context, itemIds, cancellationToken));

    /// <inheritdoc />
    public Task<Guid?> FindListingIdAsync(long listingNumber, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.FindListingIdAsync(context, listingNumber, cancellationToken));

    /// <inheritdoc />
    public Task<Guid?> FindCollectionIdAsync(long collectionNumber, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.FindCollectionIdAsync(context, collectionNumber, cancellationToken));

    /// <inheritdoc />
    public Task<int> CountPendingCollectionsAsync(Guid accountId, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.CountPendingCollectionsAsync(context, accountId, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionItemListingResult> ListItemAsync(ItemListingDraft draft, Func<ItemListingFacts, bool> isEligible, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.ListItemAsync(context, draft, isEligible, cancellationToken));

    /// <summary>
    /// Claims undelivered notifications and marks them delivered in one transaction. The rows are locked with
    /// <c>FOR UPDATE SKIP LOCKED</c>, so concurrent workers claim disjoint batches.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="batchSize">The maximum number of notifications to claim.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The claimed notifications, oldest first.</returns>
    public async Task<IReadOnlyList<AuctionNotificationRow>> ClaimUndeliveredNotificationsAsync(EntityDataContext context, int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var size = Math.Clamp(batchSize, 1, 500);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var claimed = await context.Set<AuctionNotification>()
            .FromSqlInterpolated($"SELECT * FROM data.\"AuctionNotification\" WHERE \"DeliveredAt\" IS NULL ORDER BY \"CreatedAt\" LIMIT {size} FOR UPDATE SKIP LOCKED")
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (claimed.Count > 0)
        {
            var ids = claimed.Select(n => n.Id).ToList();
            await context.Set<AuctionNotification>()
                .Where(n => ids.Contains(n.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.DeliveredAt, now), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return claimed.Select(n => new AuctionNotificationRow(n.Id, n.AccountId, n.ListingId, n.Kind, n.Currency, n.Amount, n.CreatedAt)).ToList();
    }

    /// <summary>
    /// Lists the listings of one seller, newest first. Read-only.
    /// </summary>
    /// <param name="context">The context to read with.</param>
    /// <param name="accountId">The seller's account.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">The number of listings per page.</param>
    /// <param name="status">The status to filter by, or <c>null</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One page of listings.</returns>
    public async Task<AuctionListingPage> ListSellerListingsAsync(EntityDataContext context, Guid accountId, int page, int pageSize, byte? status, CancellationToken cancellationToken = default)
    {
        var size = Math.Clamp(pageSize, 1, 50);
        var query = context.Set<AuctionListing>().AsNoTracking().Where(l => l.SellerAccountId == accountId);
        if (status is { } wanted)
        {
            query = query.Where(l => l.Status == wanted);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * size)
            .Take(size)
            .Select(l => new AuctionListingRow(
                l.Id,
                l.ListingNumber,
                l.Currency,
                l.Category,
                l.StartingPrice,
                l.CurrentBid,
                l.BuyoutPrice,
                l.EndsAt,
                l.BidCount,
                l.SellerAccountId,
                l.SellerName,
                l.ItemGroup,
                l.ItemNumber,
                l.ItemLevel,
                l.ItemId,
                l.Version,
                l.Status))
            .ToListAsync(cancellationToken);
        return new AuctionListingPage(rows, total);
    }

    /// <summary>
    /// Lists the collections of one account, newest first. Read-only.
    /// </summary>
    /// <param name="context">The context to read with.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="kind">The kind to filter by, or <c>null</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One page of mailbox entries.</returns>
    public async Task<AuctionCollectionPage> ListCollectionsAsync(EntityDataContext context, Guid accountId, int page, int pageSize, byte? kind, CancellationToken cancellationToken = default)
    {
        var size = Math.Clamp(pageSize, 1, 50);
        var query = context.Set<AuctionCollection>().AsNoTracking().Where(c => c.AccountId == accountId);
        if (kind is { } wanted)
        {
            query = query.Where(c => c.Kind == wanted);
        }

        var total = await query.CountAsync(cancellationToken);
        var stored = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * size)
            .Take(size)
            .Select(c => new
            {
                c.CollectionNumber,
                c.Kind,
                c.Status,
                c.Currency,
                c.OriginalScalar,
                c.RemainingScalar,
                c.OriginalStrength,
                c.OriginalAgility,
                c.OriginalVitality,
                c.OriginalEnergy,
                c.OriginalCommand,
                c.RemainingStrength,
                c.RemainingAgility,
                c.RemainingVitality,
                c.RemainingEnergy,
                c.RemainingCommand,
                SourceListingNumber = context.Set<AuctionListing>().Where(l => l.Id == c.ListingId).Select(l => l.ListingNumber).FirstOrDefault(),
                c.ItemId,
                c.Version,
            })
            .ToListAsync(cancellationToken);
        var rows = stored.Select(c => new AuctionCollectionRow(
            c.CollectionNumber,
            c.Kind,
            c.Status,
            c.Currency,
            c.OriginalScalar,
            c.RemainingScalar,
            c.OriginalStrength is null ? null : new FruitQuantities(c.OriginalStrength ?? 0, c.OriginalAgility ?? 0, c.OriginalVitality ?? 0, c.OriginalEnergy ?? 0, c.OriginalCommand ?? 0),
            c.RemainingStrength is null ? null : new FruitQuantities(c.RemainingStrength ?? 0, c.RemainingAgility ?? 0, c.RemainingVitality ?? 0, c.RemainingEnergy ?? 0, c.RemainingCommand ?? 0),
            c.SourceListingNumber,
            c.ItemId,
            c.Version)).ToList();
        return new AuctionCollectionPage(rows, total);
    }

    /// <summary>
    /// Loads listed items with their definition and options, so they can be serialized for the client. Read-only.
    /// </summary>
    /// <param name="context">The context to read with.</param>
    /// <param name="itemIds">The items.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The items by identifier.</returns>
    public async Task<IReadOnlyDictionary<Guid, MUnique.OpenMU.DataModel.Entities.Item>> GetItemsAsync(EntityDataContext context, IReadOnlyCollection<Guid> itemIds, CancellationToken cancellationToken = default)
    {
        var items = await context.Set<Item>().AsNoTracking()
            .Include(i => i.RawDefinition)
            .Include(i => i.RawItemOptions).ThenInclude(o => o.RawItemOption)
            .Where(i => itemIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
        return items.ToDictionary(i => i.Id, i => (MUnique.OpenMU.DataModel.Entities.Item)i);
    }

    /// <summary>
    /// Finds the identifier of a listing from its wire number.
    /// </summary>
    /// <param name="context">The context to read with.</param>
    /// <param name="listingNumber">The listing number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The listing identifier, or <c>null</c>.</returns>
    public Task<Guid?> FindListingIdAsync(EntityDataContext context, long listingNumber, CancellationToken cancellationToken = default)
    {
        return context.Set<AuctionListing>().AsNoTracking()
            .Where(l => l.ListingNumber == listingNumber)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds the identifier of a collection from its wire number.
    /// </summary>
    /// <param name="context">The context to read with.</param>
    /// <param name="collectionNumber">The collection number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The collection identifier, or <c>null</c>.</returns>
    public Task<Guid?> FindCollectionIdAsync(EntityDataContext context, long collectionNumber, CancellationToken cancellationToken = default)
    {
        return context.Set<AuctionCollection>().AsNoTracking()
            .Where(c => c.CollectionNumber == collectionNumber)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<T> WithContextAsync<T>(Func<EntityDataContext, Task<T>> operation)
    {
        await using var context = this._contextFactory();
        return await operation(context);
    }

    /// <summary>
    /// Creates a listing together with its operation record. A repeated operation returns the stored outcome.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="draft">The new listing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    public async Task<AuctionCreateResult> CreateListingAsync(EntityDataContext context, AuctionListingDraft draft, CancellationToken cancellationToken = default)
    {
        var replay = await this.FindReplayAsync(context, draft, cancellationToken);
        if (replay is not null)
        {
            return replay.Value;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var listing = new AuctionListing
        {
            Id = Guid.NewGuid(),
            ItemId = draft.ItemId,
            SellerAccountId = draft.AccountId,
            SellerCharacterId = draft.SellerCharacterId,
            SellerName = draft.SellerName,
            SellerIp = draft.SellerIp,
            Status = 0,
            Currency = draft.Currency,
            Category = draft.Category,
            StartingPrice = draft.StartingPrice,
            CreatedAt = draft.CreatedAt,
            OriginalEndsAt = draft.EndsAt,
            EndsAt = draft.EndsAt,
            NormalizedName = string.Empty,
            BuyoutPrice = draft.BuyoutPrice,
        };
        context.Set<AuctionListing>().Add(listing);
        context.Set<AuctionOperation>().Add(new AuctionOperation
        {
            Id = Guid.NewGuid(),
            AccountId = draft.AccountId,
            OperationId = draft.OperationId,
            Kind = CreateKind,
            Outcome = CreatedOutcome,
            ListingId = listing.Id,
            CreatedAt = draft.CreatedAt,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuctionCreateResult(AuctionCreateOutcome.Created, listing.Id, listing.ListingNumber);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();

            // A concurrent replay of the same operation wins the race; otherwise the item is already listed.
            replay = await this.FindReplayAsync(context, draft, cancellationToken);
            return replay ?? new AuctionCreateResult(AuctionCreateOutcome.ItemAlreadyListed, Guid.Empty, 0);
        }
    }

    /// <summary>
    /// Places a scalar bid. The listing row is locked and its version advanced by one conditional update, so two bids
    /// racing on the same version cannot both win. The displaced bid is released into a refund collection in the same
    /// transaction.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="draft">The bid.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    public async Task<AuctionBidResult> PlaceScalarBidAsync(EntityDataContext context, ScalarBidDraft draft, CancellationToken cancellationToken = default)
    {
        if (await this.HasBidOperationAsync(context, draft, cancellationToken))
        {
            return new AuctionBidResult(AuctionBidOutcome.Replayed);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var locked = await context.Set<AuctionListing>()
            .Where(l => l.Id == draft.ListingId && l.Version == draft.ExpectedVersion)
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.Version, l => l.Version + 1), cancellationToken);
        if (locked == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var exists = await context.Set<AuctionListing>().AsNoTracking().AnyAsync(l => l.Id == draft.ListingId, cancellationToken);
            return new AuctionBidResult(exists ? AuctionBidOutcome.StaleListing : AuctionBidOutcome.ListingNotFound);
        }

        var listing = await context.Set<AuctionListing>().SingleAsync(l => l.Id == draft.ListingId, cancellationToken);
        if (Refusal(listing, draft) is { } refusal)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(refusal);
        }

        if (listing.Currency != 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.UnsupportedCurrency);
        }

        if (!await OwnsCharacterAsync(context, draft.BidderAccountId, draft.BidderCharacterId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.CharacterNotOwned);
        }

        if (!await DebitZenAsync(context, draft.BidderCharacterId, draft.BidAmount, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.InsufficientCurrency);
        }

        var previous = await context.Set<AuctionBid>().SingleOrDefaultAsync(b => b.ListingId == listing.Id && b.Status == 0, cancellationToken);
        if (previous is not null)
        {
            previous.Status = 1;
            previous.ClosedAt = draft.Now;
            context.Set<AuctionCollection>().Add(new AuctionCollection
            {
                Id = Guid.NewGuid(),
                AccountId = previous.BidderAccountId,
                ListingId = listing.Id,
                Kind = 3,
                Currency = listing.Currency,
                OriginalScalar = previous.ScalarAmount,
                RemainingScalar = previous.ScalarAmount,
                Status = 0,
                CreatedAt = draft.Now,
                UpdatedAt = draft.Now,
                Version = 0,
            });
            context.Set<AuctionNotification>().Add(Notify(previous.BidderAccountId, listing.Id, OutbidNotificationKind, listing.Currency, previous.ScalarAmount, draft.Now));
        }

        var bidId = Guid.NewGuid();
        context.Set<AuctionBid>().Add(new AuctionBid
        {
            Id = bidId,
            ListingId = listing.Id,
            BidderAccountId = draft.BidderAccountId,
            BidderCharacterId = draft.BidderCharacterId,
            Currency = listing.Currency,
            ScalarAmount = draft.BidAmount,
            BidderIp = draft.BidderIp,
            Status = 0,
            CreatedAt = draft.Now,
        });
        context.Set<AuctionCurrencyMovement>().Add(Movement(draft.BidderAccountId, draft.OperationId, listing.Id, bidId, draft.BidAmount, 2, draft.Now));
        listing.CurrentBid = draft.BidAmount;
        listing.CurrentBidderAccountId = draft.BidderAccountId;
        listing.CurrentBidderCharacterId = draft.BidderCharacterId;
        listing.BidCount++;
        listing.LastBidAt = draft.Now;
        listing.EndsAt = draft.NewEndsAt;
        context.Set<AuctionOperation>().Add(new AuctionOperation
        {
            Id = Guid.NewGuid(),
            AccountId = draft.BidderAccountId,
            OperationId = draft.OperationId,
            Kind = BidKind,
            Outcome = CreatedOutcome,
            ListingId = listing.Id,
            CreatedAt = draft.Now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.Placed);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return await this.HasBidOperationAsync(context, draft, cancellationToken)
                ? new AuctionBidResult(AuctionBidOutcome.Replayed)
                : new AuctionBidResult(AuctionBidOutcome.StaleListing);
        }
    }

    /// <summary>
    /// Debits Zen from the character's inventory storage. The debit happens only when the balance covers it, in one
    /// conditional statement inside the caller's transaction.
    /// </summary>
    /// <summary>
    /// Checks that the character belongs to the account. Every Zen movement checks this, so a character can never be
    /// debited or credited on behalf of another account.
    /// </summary>
    private static async Task<bool> OwnsCharacterAsync(EntityDataContext context, Guid accountId, Guid characterId, CancellationToken cancellationToken)
    {
        var owned = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*)::int AS \"Value\" FROM data.\"Character\" WHERE \"Id\" = {characterId} AND \"AccountId\" = {accountId}")
            .SingleAsync(cancellationToken);
        return owned == 1;
    }

    private static async Task<bool> DebitZenAsync(EntityDataContext context, Guid characterId, long amount, CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return true;
        }

        var money = (int)amount;
        var rows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE data.\"ItemStorage\" s SET \"Money\" = s.\"Money\" - {money} FROM data.\"Character\" c WHERE c.\"Id\" = {characterId} AND s.\"Id\" = c.\"InventoryId\" AND s.\"Money\" >= {money}",
            cancellationToken);
        return rows == 1;
    }

    /// <summary>
    /// Credits Zen to the character's inventory storage, only while the result stays within the inventory money limit.
    /// </summary>
    private async Task<bool> CreditZenAsync(EntityDataContext context, Guid characterId, long amount, CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return true;
        }

        var money = (int)amount;
        var rows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE data.\"ItemStorage\" s SET \"Money\" = s.\"Money\" + {money} FROM data.\"Character\" c WHERE c.\"Id\" = {characterId} AND s.\"Id\" = c.\"InventoryId\" AND s.\"Money\" + {money} <= {this._moneyLimit}",
            cancellationToken);
        return rows == 1;
    }

    private static AuctionCurrencyMovement Movement(Guid accountId, Guid operationId, Guid? listingId, Guid? bidId, long amount, byte kind, DateTimeOffset now)
    {
        return new AuctionCurrencyMovement
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            OperationId = operationId,
            ListingId = listingId,
            BidId = bidId,
            Currency = 0,
            Amount = amount,
            Kind = kind,
            CreatedAt = now,
        };
    }

    private static AuctionBidOutcome? Refusal(AuctionListing listing, ScalarBidDraft draft)
    {
        if (listing.Status != 0 || listing.EndsAt <= draft.Now)
        {
            return AuctionBidOutcome.NotActive;
        }

        if (listing.SellerAccountId == draft.BidderAccountId)
        {
            return AuctionBidOutcome.SelfBid;
        }

        return draft.BidAmount < draft.MinimumBid ? AuctionBidOutcome.BidTooLow : null;
    }

    private async Task<bool> HasBidOperationAsync(EntityDataContext context, ScalarBidDraft draft, CancellationToken cancellationToken)
    {
        return await context.Set<AuctionOperation>().AsNoTracking()
            .AnyAsync(o => o.AccountId == draft.BidderAccountId && o.OperationId == draft.OperationId && o.Kind == BidKind, cancellationToken);
    }

    /// <summary>
    /// Buys a listing out at its buyout price. The listing is locked by a conditional version update. The buyer's own
    /// escrow is consumed, a displaced high bid is refunded, the buyer receives the item, and the seller receives the
    /// proceeds less the success fee, all in one transaction.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="draft">The buyout request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    public async Task<AuctionBuyoutResult> BuyoutAsync(EntityDataContext context, BuyoutDraft draft, CancellationToken cancellationToken = default)
    {
        if (await context.Set<AuctionOperation>().AsNoTracking()
                .AnyAsync(o => o.AccountId == draft.BuyerAccountId && o.OperationId == draft.OperationId && o.Kind == BuyoutKind, cancellationToken))
        {
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.Replayed);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var locked = await context.Set<AuctionListing>()
            .Where(l => l.Id == draft.ListingId && l.Version == draft.ExpectedVersion)
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.Version, l => l.Version + 1), cancellationToken);
        if (locked == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var exists = await context.Set<AuctionListing>().AsNoTracking().AnyAsync(l => l.Id == draft.ListingId, cancellationToken);
            return new AuctionBuyoutResult(exists ? AuctionBuyoutOutcome.StaleListing : AuctionBuyoutOutcome.ListingNotFound);
        }

        var listing = await context.Set<AuctionListing>().SingleAsync(l => l.Id == draft.ListingId, cancellationToken);
        if (listing.Status != 0 || listing.EndsAt <= draft.Now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.NotActive);
        }

        if (listing.SellerAccountId == draft.BuyerAccountId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.SelfBuy);
        }

        if (listing.BuyoutPrice is not { } price)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.BuyoutUnavailable);
        }

        if (listing.Currency != 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.UnsupportedCurrency);
        }

        var escrow = await context.Set<AuctionBid>().SingleOrDefaultAsync(b => b.ListingId == listing.Id && b.Status == 0, cancellationToken);
        var escrowedByBuyer = escrow is not null && escrow.BidderAccountId == draft.BuyerAccountId ? escrow.ScalarAmount ?? 0 : 0;
        var debit = price - escrowedByBuyer;
        if (!await OwnsCharacterAsync(context, draft.BuyerAccountId, draft.BuyerCharacterId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.CharacterNotOwned);
        }

        if (!await DebitZenAsync(context, draft.BuyerCharacterId, debit, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.InsufficientCurrency);
        }

        if (debit > 0)
        {
            context.Set<AuctionCurrencyMovement>().Add(Movement(draft.BuyerAccountId, draft.OperationId, listing.Id, null, debit, 2, draft.Now));
        }

        if (escrow is not null && escrow.BidderAccountId == draft.BuyerAccountId)
        {
            escrow.Status = 2;
            escrow.ClosedAt = draft.Now;
        }
        else
        {
            if (escrow is not null)
            {
                escrow.Status = 1;
                escrow.ClosedAt = draft.Now;
                context.Set<AuctionCollection>().Add(ScalarCollection(escrow.BidderAccountId, listing, 3, escrow.ScalarAmount ?? 0, draft.Now));
                context.Set<AuctionNotification>().Add(Notify(escrow.BidderAccountId, listing.Id, OutbidNotificationKind, listing.Currency, escrow.ScalarAmount, draft.Now));
            }

            context.Set<AuctionBid>().Add(new AuctionBid
            {
                Id = Guid.NewGuid(),
                ListingId = listing.Id,
                BidderAccountId = draft.BuyerAccountId,
                BidderCharacterId = draft.BuyerCharacterId,
                Currency = listing.Currency,
                ScalarAmount = price,
                BidderIp = draft.BuyerIp,
                Status = 2,
                CreatedAt = draft.Now,
                ClosedAt = draft.Now,
            });
        }

        listing.Status = 1;
        listing.CurrentBid = price;
        listing.CurrentBidderAccountId = draft.BuyerAccountId;
        listing.CurrentBidderCharacterId = draft.BuyerCharacterId;
        listing.FinalizedAt = draft.Now;
        context.Set<AuctionCollection>().Add(new AuctionCollection
        {
            Id = Guid.NewGuid(),
            AccountId = draft.BuyerAccountId,
            ListingId = listing.Id,
            Kind = 0,
            Currency = listing.Currency,
            ItemId = listing.ItemId,
            Status = 0,
            CreatedAt = draft.Now,
            UpdatedAt = draft.Now,
            Version = 0,
        });
        context.Set<AuctionCollection>().Add(ScalarCollection(listing.SellerAccountId, listing, 2, price - draft.SuccessFee, draft.Now));
        context.Set<AuctionNotification>().Add(Notify(draft.BuyerAccountId, listing.Id, BoughtNotificationKind, listing.Currency, price, draft.Now));
        context.Set<AuctionNotification>().Add(Notify(listing.SellerAccountId, listing.Id, SoldNotificationKind, listing.Currency, price, draft.Now));
        context.Set<AuctionOperation>().Add(new AuctionOperation
        {
            Id = Guid.NewGuid(),
            AccountId = draft.BuyerAccountId,
            OperationId = draft.OperationId,
            Kind = BuyoutKind,
            Outcome = CreatedOutcome,
            ListingId = listing.Id,
            CreatedAt = draft.Now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.Bought);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.StaleListing);
        }
    }

    private static AuctionCollection ScalarCollection(Guid accountId, AuctionListing listing, byte kind, long amount, DateTimeOffset now)
    {
        return new AuctionCollection
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ListingId = listing.Id,
            Kind = kind,
            Currency = listing.Currency,
            OriginalScalar = amount,
            RemainingScalar = amount,
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };
    }

    /// <summary>
    /// Cancels a listing that has no bids and returns its item to the seller through a collection.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="draft">The cancellation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    public async Task<AuctionCancelResult> CancelAsync(EntityDataContext context, CancelDraft draft, CancellationToken cancellationToken = default)
    {
        if (await context.Set<AuctionOperation>().AsNoTracking()
                .AnyAsync(o => o.AccountId == draft.AccountId && o.OperationId == draft.OperationId && o.Kind == CancelKind, cancellationToken))
        {
            return new AuctionCancelResult(AuctionCancelOutcome.Replayed);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var locked = await context.Set<AuctionListing>()
            .Where(l => l.Id == draft.ListingId && l.Version == draft.ExpectedVersion)
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.Version, l => l.Version + 1), cancellationToken);
        if (locked == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var exists = await context.Set<AuctionListing>().AsNoTracking().AnyAsync(l => l.Id == draft.ListingId, cancellationToken);
            return new AuctionCancelResult(exists ? AuctionCancelOutcome.StaleListing : AuctionCancelOutcome.ListingNotFound);
        }

        var listing = await context.Set<AuctionListing>().SingleAsync(l => l.Id == draft.ListingId, cancellationToken);
        if (listing.Status != 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionCancelResult(AuctionCancelOutcome.NotActive);
        }

        if (listing.SellerAccountId != draft.AccountId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionCancelResult(AuctionCancelOutcome.NotSeller);
        }

        if (listing.BidCount > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionCancelResult(AuctionCancelOutcome.CannotCancelWithBid);
        }

        listing.Status = 3;
        listing.FinalizedAt = draft.Now;
        context.Set<AuctionCollection>().Add(ItemCollection(listing.SellerAccountId, listing, 1, draft.Now));
        context.Set<AuctionNotification>().Add(Notify(listing.SellerAccountId, listing.Id, ReturnedNotificationKind, listing.Currency, null, draft.Now));
        context.Set<AuctionOperation>().Add(new AuctionOperation
        {
            Id = Guid.NewGuid(),
            AccountId = draft.AccountId,
            OperationId = draft.OperationId,
            Kind = CancelKind,
            Outcome = CreatedOutcome,
            ListingId = listing.Id,
            CreatedAt = draft.Now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuctionCancelResult(AuctionCancelOutcome.Cancelled);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return new AuctionCancelResult(AuctionCancelOutcome.StaleListing);
        }
    }

    /// <summary>
    /// Finalizes a listing whose expiry has passed. A listing with a bid sells to its high bidder; one without a bid returns
    /// its item to the seller. The check is a conditional update, so a listing is finalized at most once.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="draft">The expiry check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the check.</returns>
    public async Task<AuctionExpireResult> ExpireAsync(EntityDataContext context, ExpireDraft draft, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var outcome = await this.FinalizeIfDueAsync(context, draft.ListingId, draft.Now, _ => draft.SuccessFee, cancellationToken);
        if (outcome != AuctionExpireOutcome.Finalized)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionExpireResult(outcome);
        }

        await transaction.CommitAsync(cancellationToken);
        return new AuctionExpireResult(AuctionExpireOutcome.Finalized);
    }

    /// <summary>
    /// Finalizes a batch of due listings for the expiry worker. The due listings are locked with
    /// <c>FOR UPDATE SKIP LOCKED</c>, so two workers never finalize the same listing, and a listing another worker holds is
    /// skipped rather than waited for.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <param name="batchSize">The maximum number of listings to finalize in this batch.</param>
    /// <param name="successFee">The success fee for a sale price, from the domain rules.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of listings finalized.</returns>
    public async Task<int> ExpireBatchAsync(EntityDataContext context, DateTimeOffset now, int batchSize, Func<long, long> successFee, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var due = await context.Set<AuctionListing>()
            .FromSqlInterpolated($"SELECT * FROM data.\"AuctionListing\" WHERE \"Status\" = 0 AND \"EndsAt\" <= {now} ORDER BY \"EndsAt\" LIMIT {batchSize} FOR UPDATE SKIP LOCKED")
            .AsNoTracking().ToListAsync(cancellationToken);

        var finalized = 0;
        foreach (var listing in due)
        {
            await transaction.CreateSavepointAsync(ExpirySavepoint, cancellationToken);
            try
            {
                if (await this.FinalizeIfDueAsync(context, listing.Id, now, successFee, cancellationToken) == AuctionExpireOutcome.Finalized)
                {
                    finalized++;
                }

                await transaction.ReleaseSavepointAsync(ExpirySavepoint, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await transaction.RollbackToSavepointAsync(ExpirySavepoint, cancellationToken);
                await transaction.ReleaseSavepointAsync(ExpirySavepoint, cancellationToken);
                context.ChangeTracker.Clear();
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return finalized;
    }

    /// <summary>
    /// Finalizes one listing if it is due, inside the caller's transaction. The conditional update makes the finalization
    /// happen at most once even if the listing is seen twice.
    /// </summary>
    private async Task<AuctionExpireOutcome> FinalizeIfDueAsync(EntityDataContext context, Guid listingId, DateTimeOffset now, Func<long, long> successFee, CancellationToken cancellationToken)
    {
        var due = await context.Set<AuctionListing>()
            .Where(l => l.Id == listingId && l.Status == 0 && l.EndsAt <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.Version, l => l.Version + 1), cancellationToken);
        if (due == 0)
        {
            var exists = await context.Set<AuctionListing>().AsNoTracking().AnyAsync(l => l.Id == listingId, cancellationToken);
            return exists ? AuctionExpireOutcome.NotDue : AuctionExpireOutcome.ListingNotFound;
        }

        var listing = await context.Set<AuctionListing>().SingleAsync(l => l.Id == listingId, cancellationToken);
        listing.FinalizedAt = now;
        var escrow = await context.Set<AuctionBid>().SingleOrDefaultAsync(b => b.ListingId == listing.Id && b.Status == 0, cancellationToken);
        if (escrow is not null)
        {
            var price = escrow.ScalarAmount ?? 0;
            escrow.Status = 2;
            escrow.ClosedAt = now;
            listing.Status = 1;
            context.Set<AuctionCollection>().Add(ItemCollection(escrow.BidderAccountId, listing, 0, now));
            context.Set<AuctionCollection>().Add(ScalarCollection(listing.SellerAccountId, listing, 2, price - successFee(price), now));
            context.Set<AuctionNotification>().Add(Notify(escrow.BidderAccountId, listing.Id, BoughtNotificationKind, listing.Currency, price, now));
            context.Set<AuctionNotification>().Add(Notify(listing.SellerAccountId, listing.Id, SoldNotificationKind, listing.Currency, price, now));
        }
        else
        {
            listing.Status = 2;
            context.Set<AuctionCollection>().Add(ItemCollection(listing.SellerAccountId, listing, 1, now));
            context.Set<AuctionNotification>().Add(Notify(listing.SellerAccountId, listing.Id, ExpiredNotificationKind, listing.Currency, null, now));
        }

        await context.SaveChangesAsync(cancellationToken);
        return AuctionExpireOutcome.Finalized;
    }

    /// <summary>
    /// Creates a durable notification for an account. Online delivery is best-effort; the Mailbox stays the durable truth.
    /// </summary>
    private static AuctionNotification Notify(Guid accountId, Guid listingId, byte kind, byte currency, long? amount, DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ListingId = listingId,
            Kind = kind,
            Currency = currency,
            Amount = amount,
            CreatedAt = now,
        };

    private static AuctionCollection ItemCollection(Guid accountId, AuctionListing listing, byte kind, DateTimeOffset now)
    {
        return new AuctionCollection
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ListingId = listing.Id,
            Kind = kind,
            Currency = listing.Currency,
            ItemId = listing.ItemId,
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };
    }

    /// <summary>
    /// Claims part or all of a collection for its owning account. The collection's version is advanced by one conditional
    /// update, so a claim on an outdated version, or a racing claim, changes nothing. A Zen collection is claimed in full and
    /// credited to the character; a jewel collection is claimed in the requested quantity; a fruit-basket collection is claimed
    /// component by component.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="draft">The claim request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, with what the game server must deliver.</returns>
    public async Task<AuctionClaimResult> ClaimAsync(EntityDataContext context, ClaimDraft draft, CancellationToken cancellationToken = default)
    {
        if (await context.Set<AuctionOperation>().AsNoTracking()
                .AnyAsync(o => o.AccountId == draft.AccountId && o.OperationId == draft.OperationId && o.Kind == ClaimKind, cancellationToken))
        {
            return new AuctionClaimResult(AuctionClaimOutcome.Replayed, 0, null, 0);
        }

        var collection = await context.Set<AuctionCollection>().AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == draft.CollectionId && c.AccountId == draft.AccountId, cancellationToken);
        if (collection is null)
        {
            return new AuctionClaimResult(AuctionClaimOutcome.NotFound, 0, null, 0);
        }

        if (collection.Version != draft.ExpectedVersion)
        {
            return new AuctionClaimResult(AuctionClaimOutcome.Stale, 0, null, 0);
        }

        if (collection.Status == 2)
        {
            return new AuctionClaimResult(AuctionClaimOutcome.AlreadyClaimed, 0, null, 0);
        }

        if (collection.ItemId is { } lotItemId)
        {
            return await this.ClaimLotAsync(context, draft, collection, lotItemId, cancellationToken);
        }

        if (Plan(collection, draft) is not { } plan)
        {
            return new AuctionClaimResult(AuctionClaimOutcome.InvalidClaimQuantity, 0, null, 0);
        }

        var status = plan.Status;
        var scalar = plan.Scalar;
        var strength = plan.RemainingFruits?.Strength;
        var agility = plan.RemainingFruits?.Agility;
        var vitality = plan.RemainingFruits?.Vitality;
        var energy = plan.RemainingFruits?.Energy;
        var command = plan.RemainingFruits?.Command;
        var now = draft.Now;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var updated = await context.Set<AuctionCollection>()
            .Where(c => c.Id == draft.CollectionId && c.AccountId == draft.AccountId && c.Version == draft.ExpectedVersion && c.Status != 2)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Status, status)
                .SetProperty(c => c.UpdatedAt, now)
                .SetProperty(c => c.Version, c => c.Version + 1)
                .SetProperty(c => c.RemainingScalar, scalar)
                .SetProperty(c => c.RemainingStrength, strength)
                .SetProperty(c => c.RemainingAgility, agility)
                .SetProperty(c => c.RemainingVitality, vitality)
                .SetProperty(c => c.RemainingEnergy, energy)
                .SetProperty(c => c.RemainingCommand, command), cancellationToken);
        if (updated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionClaimResult(AuctionClaimOutcome.Stale, 0, null, 0);
        }

        if (collection.Currency == 0 && plan.ClaimedScalar > 0)
        {
            if (!await OwnsCharacterAsync(context, draft.AccountId, draft.CharacterId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AuctionClaimResult(AuctionClaimOutcome.NotFound, 0, null, 0);
            }

            if (!await this.CreditZenAsync(context, draft.CharacterId, plan.ClaimedScalar, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AuctionClaimResult(AuctionClaimOutcome.MoneyLimit, 0, null, 0);
            }
        }

        if (plan.ClaimedScalar > 0)
        {
            var movement = Movement(draft.AccountId, draft.OperationId, null, null, plan.ClaimedScalar, 6, now);
            movement.CollectionId = collection.Id;
            movement.Currency = collection.Currency;
            context.Set<AuctionCurrencyMovement>().Add(movement);
        }

        if (plan.ClaimedFruits is { } fruits)
        {
            var components = new[] { fruits.Strength, fruits.Agility, fruits.Vitality, fruits.Energy, fruits.Command };
            for (byte index = 0; index < components.Length; index++)
            {
                if (components[index] <= 0)
                {
                    continue;
                }

                var movement = Movement(draft.AccountId, draft.OperationId, null, null, components[index], 6, now);
                movement.CollectionId = collection.Id;
                movement.Currency = collection.Currency;
                movement.FruitComponent = index;
                context.Set<AuctionCurrencyMovement>().Add(movement);
            }
        }

        context.Set<AuctionOperation>().Add(new AuctionOperation
        {
            Id = Guid.NewGuid(),
            AccountId = draft.AccountId,
            OperationId = draft.OperationId,
            Kind = ClaimKind,
            Outcome = CreatedOutcome,
            CollectionId = collection.Id,
            CreatedAt = now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuctionClaimResult(AuctionClaimOutcome.Claimed, collection.Kind, collection.ItemId, plan.ClaimedScalar);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return new AuctionClaimResult(AuctionClaimOutcome.Replayed, 0, null, 0);
        }
    }

    /// <summary>
    /// The validated effect of a claim on a collection.
    /// </summary>
    /// <param name="Status">The collection status after the claim, as its wire value.</param>
    /// <param name="Scalar">The remaining scalar amount after the claim, for scalar collections.</param>
    /// <param name="RemainingFruits">The remaining fruit quantities after the claim, for fruit-basket collections.</param>
    /// <param name="ClaimedScalar">The scalar amount claimed now.</param>
    /// <param name="ClaimedFruits">The fruit quantities claimed now, for fruit-basket collections.</param>
    private readonly record struct ClaimPlan(byte Status, long? Scalar, FruitQuantities? RemainingFruits, long ClaimedScalar, FruitQuantities? ClaimedFruits);

    /// <summary>
    /// Returns an auctioned item to the claimant's main inventory. The character must belong to the claiming account, the
    /// item must still be outside every inventory, and the collection is marked claimed in the same transaction.
    /// </summary>
    private async Task<AuctionClaimResult> ClaimLotAsync(EntityDataContext context, ClaimDraft draft, AuctionCollection collection, Guid itemId, CancellationToken cancellationToken)
    {
        if (draft.RequestedScalar is not null || draft.RequestedFruits is not null)
        {
            return new AuctionClaimResult(AuctionClaimOutcome.InvalidClaimQuantity, 0, null, 0);
        }

        var owned = await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*)::int AS \"Value\" FROM data.\"Character\" WHERE \"Id\" = {draft.CharacterId} AND \"AccountId\" = {draft.AccountId}")
            .SingleAsync(cancellationToken);
        if (owned == 0)
        {
            return new AuctionClaimResult(AuctionClaimOutcome.NotFound, 0, null, 0);
        }

        var now = draft.Now;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var freeSlot = await context.Database.SqlQuery<int?>(
            $"SELECT MIN(s.n) AS \"Value\" FROM generate_series({InventoryConstants.EquippableSlotsCount}, {InventoryConstants.EquippableSlotsCount + 63}) AS s(n) WHERE s.n NOT IN (SELECT i.\"ItemSlot\" FROM data.\"Item\" i JOIN data.\"Character\" c ON i.\"ItemStorageId\" = c.\"InventoryId\" WHERE c.\"Id\" = {draft.CharacterId})")
            .SingleAsync(cancellationToken);
        if (freeSlot is not { } slot)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionClaimResult(AuctionClaimOutcome.InventoryFull, 0, null, 0);
        }

        var placed = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE data.\"Item\" i SET \"ItemStorageId\" = c.\"InventoryId\", \"ItemSlot\" = {(short)slot} FROM data.\"Character\" c WHERE c.\"Id\" = {draft.CharacterId} AND c.\"AccountId\" = {draft.AccountId} AND i.\"Id\" = {itemId} AND i.\"ItemStorageId\" IS NULL",
            cancellationToken);
        if (placed != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionClaimResult(AuctionClaimOutcome.Stale, 0, null, 0);
        }

        var claimed = await context.Set<AuctionCollection>()
            .Where(c => c.Id == collection.Id && c.AccountId == draft.AccountId && c.Version == draft.ExpectedVersion && c.Status != 2)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Status, (byte)2)
                .SetProperty(c => c.UpdatedAt, now)
                .SetProperty(c => c.Version, c => c.Version + 1), cancellationToken);
        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionClaimResult(AuctionClaimOutcome.Stale, 0, null, 0);
        }

        context.Set<AuctionOperation>().Add(new AuctionOperation
        {
            Id = Guid.NewGuid(),
            AccountId = draft.AccountId,
            OperationId = draft.OperationId,
            Kind = ClaimKind,
            Outcome = CreatedOutcome,
            CollectionId = collection.Id,
            CreatedAt = now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuctionClaimResult(AuctionClaimOutcome.Claimed, collection.Kind, itemId, 0);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return new AuctionClaimResult(AuctionClaimOutcome.Replayed, 0, null, 0);
        }
    }

    private static ClaimPlan? Plan(AuctionCollection collection, ClaimDraft draft)
    {
        if (collection.RemainingScalar is { } remaining)
        {
            var requested = draft.RequestedScalar ?? remaining;
            if (requested <= 0 || requested > remaining)
            {
                return null;
            }

            if (collection.Currency == 0 && requested != remaining)
            {
                return null;
            }

            var left = remaining - requested;
            return new ClaimPlan(left == 0 ? (byte)2 : (byte)1, left, null, requested, null);
        }

        if (collection.RemainingStrength is not null)
        {
            var available = new FruitQuantities(
                collection.RemainingStrength ?? 0,
                collection.RemainingAgility ?? 0,
                collection.RemainingVitality ?? 0,
                collection.RemainingEnergy ?? 0,
                collection.RemainingCommand ?? 0);
            var requested = draft.RequestedFruits ?? available;
            if (requested.Strength < 0 || requested.Strength > available.Strength
                || requested.Agility < 0 || requested.Agility > available.Agility
                || requested.Vitality < 0 || requested.Vitality > available.Vitality
                || requested.Energy < 0 || requested.Energy > available.Energy
                || requested.Command < 0 || requested.Command > available.Command
                || (requested.Strength + requested.Agility + requested.Vitality + requested.Energy + requested.Command) == 0)
            {
                return null;
            }

            var left = new FruitQuantities(
                available.Strength - requested.Strength,
                available.Agility - requested.Agility,
                available.Vitality - requested.Vitality,
                available.Energy - requested.Energy,
                available.Command - requested.Command);
            var empty = left.Strength + left.Agility + left.Vitality + left.Energy + left.Command == 0;
            return new ClaimPlan(empty ? (byte)2 : (byte)1, null, left, 0, requested);
        }

        return new ClaimPlan(2, null, null, 0, null);
    }

    /// <summary>
    /// Searches active listings of one currency. Results are ordered and paged, and the total count covers every match.
    /// </summary>
    /// <param name="context">The context to read from.</param>
    /// <param name="search">The browse request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The requested page of listings.</returns>
    /// <exception cref="ArgumentException">A price sort is requested for fruit baskets.</exception>
    public async Task<AuctionListingPage> SearchListingsAsync(EntityDataContext context, AuctionListingSearch search, CancellationToken cancellationToken = default)
    {
        const byte FruitsCurrency = 8;
        if (search.Currency == FruitsCurrency && search.Sort is AuctionSearchSort.PriceAscending or AuctionSearchSort.PriceDescending)
        {
            throw new ArgumentException("Fruit baskets have no price order.", nameof(search));
        }

        var query = context.Set<AuctionListing>().AsNoTracking().Where(l => l.Status == 0 && l.Currency == search.Currency);
        if (search.Category is { } category)
        {
            query = query.Where(l => l.Category == category);
        }

        var total = await query.CountAsync(cancellationToken);
        IOrderedQueryable<AuctionListing> ordered = search.Sort switch
        {
            AuctionSearchSort.PriceAscending => query.OrderBy(l => l.CurrentBid ?? l.StartingPrice).ThenBy(l => l.ListingNumber),
            AuctionSearchSort.PriceDescending => query.OrderByDescending(l => l.CurrentBid ?? l.StartingPrice).ThenBy(l => l.ListingNumber),
            AuctionSearchSort.Newest => query.OrderByDescending(l => l.ListingNumber),
            _ => query.OrderBy(l => l.EndsAt).ThenBy(l => l.ListingNumber),
        };

        var pageNumber = Math.Max(1, search.Page);
        var size = Math.Max(1, search.PageSize);
        var rows = await ordered
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(l => new AuctionListingRow(
                l.Id,
                l.ListingNumber,
                l.Currency,
                l.Category,
                l.StartingPrice,
                l.CurrentBid,
                l.BuyoutPrice,
                l.EndsAt,
                l.BidCount,
                l.SellerAccountId,
                l.SellerName,
                l.ItemGroup,
                l.ItemNumber,
                l.ItemLevel,
                l.ItemId,
                l.Version,
                l.Status))
            .ToListAsync(cancellationToken);
        return new AuctionListingPage(rows, total);
    }

    /// <summary>
    /// Lists an inventory item in one transaction. The item is located by its slot and checked inside the transaction, the
    /// listing fee is charged in Zen, the item leaves the inventory, and the listing, operation, and fee movement are written.
    /// A repeated operation returns its stored listing without charging again.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="draft">The listing values.</param>
    /// <param name="isEligible">Decides whether the item may be listed, from its facts as read inside the transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    public async Task<AuctionItemListingResult> ListItemAsync(EntityDataContext context, ItemListingDraft draft, Func<ItemListingFacts, bool> isEligible, CancellationToken cancellationToken = default)
    {
        var replay = await this.FindReplayAsync(context, draft.AccountId, draft.OperationId, cancellationToken);
        if (replay is { } stored)
        {
            return new AuctionItemListingResult(AuctionItemListingOutcome.Replayed, stored.ListingId, stored.ListingNumber);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var inventoryId = await context.Set<Character>().AsNoTracking()
            .Where(c => c.Id == draft.CharacterId)
            .Select(c => c.InventoryId)
            .SingleOrDefaultAsync(cancellationToken);
        var item = inventoryId is null
            ? null
            : await context.Set<Item>().Include(i => i.RawDefinition)
                .SingleOrDefaultAsync(i => i.ItemStorageId == inventoryId && i.ItemSlot == draft.InventorySlot, cancellationToken);
        if (item is null)
        {
            return new AuctionItemListingResult(AuctionItemListingOutcome.ItemNotFound, Guid.Empty, 0);
        }

        var referenced = await context.Set<AuctionListing>().AnyAsync(l => l.ItemId == item.Id && l.Status == 0, cancellationToken)
            || await context.Set<AuctionCollection>().AnyAsync(c => c.ItemId == item.Id && c.Status != 2, cancellationToken);
        var definition = item.RawDefinition;
        var facts = new ItemListingFacts(
            item.Id,
            definition?.IsTradable ?? false,
            definition?.IsPersonalStoreSellable ?? false,
            definition?.IsBoundToCharacter ?? true,
            definition?.IsQuestItem ?? true,
            (int)Math.Ceiling(item.Durability),
            item.ItemSlot,
            referenced);
        if (!isEligible(facts))
        {
            return new AuctionItemListingResult(AuctionItemListingOutcome.NotEligible, Guid.Empty, 0);
        }

        var accountListings = await context.Set<AuctionListing>().CountAsync(l => l.SellerAccountId == draft.AccountId && l.Status == 0, cancellationToken);
        var characterListings = await context.Set<AuctionListing>().CountAsync(l => l.SellerCharacterId == draft.CharacterId && l.Status == 0, cancellationToken);
        if (accountListings >= draft.ListingLimitPerAccount || characterListings >= draft.ListingLimitPerCharacter)
        {
            return new AuctionItemListingResult(AuctionItemListingOutcome.ListingLimitReached, Guid.Empty, 0);
        }

        if (!await OwnsCharacterAsync(context, draft.AccountId, draft.CharacterId, cancellationToken))
        {
            return new AuctionItemListingResult(AuctionItemListingOutcome.CharacterNotOwned, Guid.Empty, 0);
        }

        if (!await DebitZenAsync(context, draft.CharacterId, draft.ListingFee, cancellationToken))
        {
            return new AuctionItemListingResult(AuctionItemListingOutcome.InsufficientCurrency, Guid.Empty, 0);
        }

        var listingId = Guid.NewGuid();
        context.Set<AuctionListing>().Add(new AuctionListing
        {
            Id = listingId,
            ItemId = item.Id,
            SellerAccountId = draft.AccountId,
            SellerCharacterId = draft.CharacterId,
            SellerName = draft.SellerName,
            SellerIp = draft.SellerIp,
            Status = 0,
            Currency = draft.Currency,
            Category = draft.Category,
            StartingPrice = draft.StartingPrice,
            BuyoutPrice = draft.BuyoutPrice,
            CreatedAt = draft.CreatedAt,
            OriginalEndsAt = draft.EndsAt,
            EndsAt = draft.EndsAt,
            NormalizedName = string.Empty,
        });
        context.Set<AuctionOperation>().Add(new AuctionOperation
        {
            Id = Guid.NewGuid(),
            AccountId = draft.AccountId,
            OperationId = draft.OperationId,
            Kind = CreateKind,
            Outcome = CreatedOutcome,
            ListingId = listingId,
            CreatedAt = draft.CreatedAt,
        });
        context.Set<AuctionCurrencyMovement>().Add(Movement(draft.AccountId, draft.OperationId, listingId, null, draft.ListingFee, FeeRemovedKind, draft.CreatedAt));
        item.ItemStorageId = null;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();

            // A concurrent replay of the same operation wins the race; otherwise the item is already listed.
            replay = await this.FindReplayAsync(context, draft.AccountId, draft.OperationId, cancellationToken);
            return replay is { } raced
                ? new AuctionItemListingResult(AuctionItemListingOutcome.Replayed, raced.ListingId, raced.ListingNumber)
                : new AuctionItemListingResult(AuctionItemListingOutcome.ItemAlreadyListed, Guid.Empty, 0);
        }

        var listingNumber = await context.Set<AuctionListing>().AsNoTracking()
            .Where(l => l.Id == listingId)
            .Select(l => l.ListingNumber)
            .SingleAsync(cancellationToken);
        return new AuctionItemListingResult(AuctionItemListingOutcome.Created, listingId, listingNumber);
    }

    private Task<AuctionCreateResult?> FindReplayAsync(EntityDataContext context, AuctionListingDraft draft, CancellationToken cancellationToken)
        => this.FindReplayAsync(context, draft.AccountId, draft.OperationId, cancellationToken);

    private async Task<AuctionCreateResult?> FindReplayAsync(EntityDataContext context, Guid accountId, Guid operationId, CancellationToken cancellationToken)
    {
        var operation = await context.Set<AuctionOperation>().AsNoTracking()
            .FirstOrDefaultAsync(o => o.AccountId == accountId && o.OperationId == operationId, cancellationToken);
        if (operation is null || operation.ListingId is not { } listingId)
        {
            return null;
        }

        var listingNumber = await context.Set<AuctionListing>().AsNoTracking()
            .Where(l => l.Id == listingId)
            .Select(l => l.ListingNumber)
            .SingleAsync(cancellationToken);
        return new AuctionCreateResult(AuctionCreateOutcome.Replayed, listingId, listingNumber);
    }

    /// <summary>
    /// Reads one listing, without locking it. Used for the detail view only.
    /// </summary>
    /// <param name="context">The context to read with.</param>
    /// <param name="listingId">The listing to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The listing, or <c>null</c> when it does not exist.</returns>
    public Task<AuctionListingDetail?> GetListingAsync(EntityDataContext context, Guid listingId, CancellationToken cancellationToken = default)
    {
        return context.Set<AuctionListing>().AsNoTracking()
            .Where(l => l.Id == listingId)
            .Select(l => new AuctionListingDetail(l.Id, l.Version, l.Status, l.Currency, l.Category, l.ItemId, l.StartingPrice, l.BuyoutPrice, l.CurrentBid, l.EndsAt, l.OriginalEndsAt, l.BidCount, l.SellerName, l.Note))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Counts the collections of an account which are not fully claimed yet.
    /// </summary>
    /// <param name="context">The context to read with.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of pending collections.</returns>
    public Task<int> CountPendingCollectionsAsync(EntityDataContext context, Guid accountId, CancellationToken cancellationToken = default)
    {
        return context.Set<AuctionCollection>().AsNoTracking().CountAsync(c => c.AccountId == accountId && c.Status != 2, cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
