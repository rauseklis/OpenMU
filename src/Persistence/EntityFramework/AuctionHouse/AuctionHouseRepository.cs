// <copyright file="AuctionHouseRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using Npgsql;

/// <summary>
/// The outcome of a listing creation.
/// </summary>
public enum AuctionCreateOutcome
{
    /// <summary>
    /// The listing was created.
    /// </summary>
    Created,

    /// <summary>
    /// The operation was already processed; the stored outcome is returned and nothing was created.
    /// </summary>
    Replayed,

    /// <summary>
    /// The item already has an active listing.
    /// </summary>
    ItemAlreadyListed,
}

/// <summary>
/// The result of a listing creation.
/// </summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="ListingId">The listing identifier, or an empty identifier when the listing was refused.</param>
/// <param name="ListingNumber">The 64-bit listing number used on the wire, or zero when the listing was refused.</param>
public readonly record struct AuctionCreateResult(AuctionCreateOutcome Outcome, Guid ListingId, long ListingNumber);

/// <summary>
/// The values for a new listing, along with the operation which requested it.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="AccountId">The seller's account.</param>
/// <param name="ItemId">The item to list.</param>
/// <param name="SellerCharacterId">The seller's character.</param>
/// <param name="SellerName">The seller's character name.</param>
/// <param name="SellerIp">The seller's IP address.</param>
/// <param name="Currency">The currency mode as its wire value.</param>
/// <param name="Category">The category as its wire value.</param>
/// <param name="StartingPrice">The scalar starting price.</param>
/// <param name="CreatedAt">The creation time, in UTC.</param>
/// <param name="EndsAt">The expiry, in UTC.</param>
public readonly record struct AuctionListingDraft(
    Guid OperationId,
    Guid AccountId,
    Guid ItemId,
    Guid SellerCharacterId,
    string SellerName,
    string? SellerIp,
    byte Currency,
    byte Category,
    long StartingPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset EndsAt);

/// <summary>
/// Persists Auction House operations. Each operation runs in one database transaction, and its operation record is
/// written with it, so an operation either fully happens or not at all.
/// </summary>
public sealed class AuctionHouseRepository
{
    private const byte CreateKind = 0;
    private const int CreatedOutcome = 0;
    private const byte BidKind = 1;

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
        }

        context.Set<AuctionBid>().Add(new AuctionBid
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            BidderAccountId = draft.BidderAccountId,
            BidderCharacterId = draft.BidderCharacterId,
            Currency = listing.Currency,
            ScalarAmount = draft.BidAmount,
            BidderIp = draft.BidderIp,
            Status = 0,
            CreatedAt = draft.Now,
        });
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

    private async Task<AuctionCreateResult?> FindReplayAsync(EntityDataContext context, AuctionListingDraft draft, CancellationToken cancellationToken)
    {
        var operation = await context.Set<AuctionOperation>().AsNoTracking()
            .FirstOrDefaultAsync(o => o.AccountId == draft.AccountId && o.OperationId == draft.OperationId, cancellationToken);
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

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
