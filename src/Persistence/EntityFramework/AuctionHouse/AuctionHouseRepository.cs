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
    DateTimeOffset EndsAt,
    long? BuyoutPrice = null);

/// <summary>
/// Persists Auction House operations. Each operation runs in one database transaction, and its operation record is
/// written with it, so an operation either fully happens or not at all.
/// </summary>
public sealed class AuctionHouseRepository
{
    private const byte CreateKind = 0;
    private const int CreatedOutcome = 0;
    private const byte BidKind = 1;
    private const byte BuyoutKind = 2;
    private const byte CancelKind = 3;
    private const byte ClaimKind = 4;

    /// <summary>
    /// The inventory money limit. This is a placeholder until the server's configured limit is wired in.
    /// </summary>
    private const int MoneyLimit = 2_000_000_000;

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
    private static async Task<bool> CreditZenAsync(EntityDataContext context, Guid characterId, long amount, CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return true;
        }

        var money = (int)amount;
        var rows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE data.\"ItemStorage\" s SET \"Money\" = s.\"Money\" + {money} FROM data.\"Character\" c WHERE c.\"Id\" = {characterId} AND s.\"Id\" = c.\"InventoryId\" AND s.\"Money\" + {money} <= {MoneyLimit}",
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
        var due = await context.Set<AuctionListing>()
            .Where(l => l.Id == draft.ListingId && l.Status == 0 && l.EndsAt <= draft.Now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.Version, l => l.Version + 1), cancellationToken);
        if (due == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var exists = await context.Set<AuctionListing>().AsNoTracking().AnyAsync(l => l.Id == draft.ListingId, cancellationToken);
            return new AuctionExpireResult(exists ? AuctionExpireOutcome.NotDue : AuctionExpireOutcome.ListingNotFound);
        }

        var listing = await context.Set<AuctionListing>().SingleAsync(l => l.Id == draft.ListingId, cancellationToken);
        listing.FinalizedAt = draft.Now;
        var escrow = await context.Set<AuctionBid>().SingleOrDefaultAsync(b => b.ListingId == listing.Id && b.Status == 0, cancellationToken);
        if (escrow is not null)
        {
            escrow.Status = 2;
            escrow.ClosedAt = draft.Now;
            listing.Status = 1;
            context.Set<AuctionCollection>().Add(ItemCollection(escrow.BidderAccountId, listing, 0, draft.Now));
            context.Set<AuctionCollection>().Add(ScalarCollection(listing.SellerAccountId, listing, 2, (escrow.ScalarAmount ?? 0) - draft.SuccessFee, draft.Now));
        }
        else
        {
            listing.Status = 2;
            context.Set<AuctionCollection>().Add(ItemCollection(listing.SellerAccountId, listing, 1, draft.Now));
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AuctionExpireResult(AuctionExpireOutcome.Finalized);
    }

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
    /// Claims a collection for its owning account. The collection is marked claimed by one conditional update, so it can
    /// be claimed once for a given version, and a racing or repeated claim changes nothing.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="draft">The claim request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, with the item or amount to deliver.</returns>
    public async Task<AuctionClaimResult> ClaimAsync(EntityDataContext context, ClaimDraft draft, CancellationToken cancellationToken = default)
    {
        if (await context.Set<AuctionOperation>().AsNoTracking()
                .AnyAsync(o => o.AccountId == draft.AccountId && o.OperationId == draft.OperationId && o.Kind == ClaimKind, cancellationToken))
        {
            return new AuctionClaimResult(AuctionClaimOutcome.Replayed, 0, null, 0);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var claimed = await context.Set<AuctionCollection>()
            .Where(c => c.Id == draft.CollectionId && c.AccountId == draft.AccountId && c.Version == draft.ExpectedVersion && c.Status != 2)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Status, (byte)2)
                .SetProperty(c => c.UpdatedAt, draft.Now)
                .SetProperty(c => c.Version, c => c.Version + 1), cancellationToken);
        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var current = await context.Set<AuctionCollection>().AsNoTracking()
                .Where(c => c.Id == draft.CollectionId && c.AccountId == draft.AccountId)
                .Select(c => new { c.Version })
                .SingleOrDefaultAsync(cancellationToken);
            if (current is null)
            {
                return new AuctionClaimResult(AuctionClaimOutcome.NotFound, 0, null, 0);
            }

            return new AuctionClaimResult(current.Version == draft.ExpectedVersion ? AuctionClaimOutcome.AlreadyClaimed : AuctionClaimOutcome.Stale, 0, null, 0);
        }

        var collection = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.Id == draft.CollectionId, cancellationToken);
        if (collection.Currency == 0 && collection.Kind is 2 or 3 && (collection.RemainingScalar ?? 0) > 0)
        {
            var amount = collection.RemainingScalar ?? 0;
            if (!await CreditZenAsync(context, draft.CharacterId, amount, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AuctionClaimResult(AuctionClaimOutcome.MoneyLimit, 0, null, 0);
            }

            var credit = Movement(draft.AccountId, draft.OperationId, null, null, amount, 6, draft.Now);
            credit.CollectionId = collection.Id;
            context.Set<AuctionCurrencyMovement>().Add(credit);
        }

        context.Set<AuctionOperation>().Add(new AuctionOperation
        {
            Id = Guid.NewGuid(),
            AccountId = draft.AccountId,
            OperationId = draft.OperationId,
            Kind = ClaimKind,
            Outcome = CreatedOutcome,
            CollectionId = collection.Id,
            CreatedAt = draft.Now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuctionClaimResult(AuctionClaimOutcome.Claimed, collection.Kind, collection.ItemId, collection.RemainingScalar ?? 0);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return new AuctionClaimResult(AuctionClaimOutcome.Replayed, 0, null, 0);
        }
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
