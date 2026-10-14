// <copyright file="AuctionHouseRepository.Mixed.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// The mixed-currency part of the repository: listings priced in a basket of Zen, jewels and stat fruits. A basket is paid, refunded,
/// credited and claimed whole: every component moves or none does, because each operation runs in one transaction.
/// </summary>
public sealed partial class AuctionHouseRepository
{
    private const byte MixedCurrency = 9;
    private const byte EscrowMovementKind = 2;
    private const byte ClaimMovementKind = 6;
    private const byte RefundCollectionKind = 3;
    private const byte ProceedsCollectionKind = 2;
    private const byte ItemToBuyerCollectionKind = 0;
    private const string EmptyVector = "0,0,0,0,0,0,0,0,0,0,0,0,0";

    /// <inheritdoc />
    public Task<AuctionBidResult> PlaceMixedBidAsync(MixedBidDraft draft, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.PlaceMixedBidAsync(context, draft, cancellationToken));

    /// <inheritdoc />
    public Task<AuctionBuyoutResult> PlaceMixedBuyoutAsync(MixedBuyoutDraft draft, CancellationToken cancellationToken = default)
        => this.WithContextAsync(context => this.PlaceMixedBuyoutAsync(context, draft, cancellationToken));

    /// <summary>
    /// Places a bid on a mixed-currency listing. The bid must satisfy the dominance rule against the starting price and the
    /// current bid. Every component of the bid is escrowed or none is, and the displaced bid is released into a refund
    /// collection in the same transaction.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="draft">The bid.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    public async Task<AuctionBidResult> PlaceMixedBidAsync(EntityDataContext context, MixedBidDraft draft, CancellationToken cancellationToken = default)
    {
        if (await context.Set<AuctionOperation>().AsNoTracking()
                .AnyAsync(o => o.AccountId == draft.BidderAccountId && o.OperationId == draft.OperationId && o.Kind == BidKind, cancellationToken))
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
        if (listing.Currency != MixedCurrency)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.UnsupportedCurrency);
        }

        if (listing.Status != 0 || listing.EndsAt <= draft.Now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.NotActive);
        }

        if (listing.SellerAccountId == draft.BidderAccountId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.SelfBid);
        }

        if (PriceBasket.Parse(listing.PriceVector) is not { } starting || starting.IsEmpty)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.NotBiddable);
        }

        if (!PriceRules.IsBidAllowed(starting, PriceBasket.Parse(listing.CurrentBidVector), draft.Bid, draft.Limits))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.BidTooLow);
        }

        if (!await OwnsCharacterAsync(context, draft.BidderAccountId, draft.BidderCharacterId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.CharacterNotOwned);
        }

        if (!await this.TenderBasketAsync(context, draft.BidderAccountId, draft.BidderCharacterId, draft.OperationId, listing.Id, draft.Bid, draft.Now, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBidResult(AuctionBidOutcome.InsufficientCurrency);
        }

        var previous = await context.Set<AuctionBid>().SingleOrDefaultAsync(b => b.ListingId == listing.Id && b.Status == 0, cancellationToken);
        if (previous is not null)
        {
            previous.Status = 1;
            previous.ClosedAt = draft.Now;
            context.Set<AuctionCollection>().Add(MixedCollection(previous.BidderAccountId, listing.Id, RefundCollectionKind, PriceBasket.Parse(previous.AmountVector) ?? PriceBasket.Empty, draft.Now));
            context.Set<AuctionNotification>().Add(Notify(previous.BidderAccountId, listing.Id, OutbidNotificationKind, MixedCurrency, null, draft.Now));
        }

        var bidId = Guid.NewGuid();
        context.Set<AuctionBid>().Add(new AuctionBid
        {
            Id = bidId,
            ListingId = listing.Id,
            BidderAccountId = draft.BidderAccountId,
            BidderCharacterId = draft.BidderCharacterId,
            Currency = MixedCurrency,
            AmountVector = draft.Bid.ToVectorText(),
            BidderIp = draft.BidderIp,
            Status = 0,
            CreatedAt = draft.Now,
        });
        AddComponentMovements(context, draft.Bid, draft.BidderAccountId, draft.OperationId, listing.Id, bidId, EscrowMovementKind, draft.Now);
        listing.CurrentBid = null;
        listing.CurrentBidVector = draft.Bid.ToVectorText();
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
        context.Set<AuctionTransactionLog>().Add(Audit(BidKind, draft.BidderAccountId, draft.BidderCharacterId, listing.Id, draft.OperationId, draft.Now, draft.BidderIp, listing.SellerAccountId, null, MixedCurrency, null, SameAddress(draft.BidderIp, listing.SellerIp)));

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
            return new AuctionBidResult(AuctionBidOutcome.StaleListing);
        }
    }

    /// <summary>
    /// Buys a mixed-currency listing out. The buyer pays the buyout basket less the part they already escrowed as the high bidder
    /// (anything they escrowed beyond the buyout is returned), another bidder's escrow is refunded, and the seller receives the
    /// buyout basket. No percentage fee is taken: the flat sale fee held at listing is consumed instead.
    /// </summary>
    /// <param name="context">The context to write with.</param>
    /// <param name="draft">The buyout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome of the request.</returns>
    public async Task<AuctionBuyoutResult> PlaceMixedBuyoutAsync(EntityDataContext context, MixedBuyoutDraft draft, CancellationToken cancellationToken = default)
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
        if (listing.Currency != MixedCurrency)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.UnsupportedCurrency);
        }

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

        if (PriceBasket.Parse(listing.BuyoutVector) is not { } buyout || buyout.IsEmpty)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.BuyoutUnavailable);
        }

        if (!await OwnsCharacterAsync(context, draft.BuyerAccountId, draft.BuyerCharacterId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.CharacterNotOwned);
        }

        var escrow = await context.Set<AuctionBid>().SingleOrDefaultAsync(b => b.ListingId == listing.Id && b.Status == 0, cancellationToken);
        var ownEscrow = escrow is not null && escrow.BidderAccountId == draft.BuyerAccountId
            ? PriceBasket.Parse(escrow.AmountVector) ?? PriceBasket.Empty
            : PriceBasket.Empty;
        var debit = buyout.Minus(ownEscrow);
        var excess = ownEscrow.Minus(buyout);
        if (!debit.IsEmpty && !await this.TenderBasketAsync(context, draft.BuyerAccountId, draft.BuyerCharacterId, draft.OperationId, listing.Id, debit, draft.Now, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionBuyoutResult(AuctionBuyoutOutcome.InsufficientCurrency);
        }

        AddComponentMovements(context, debit, draft.BuyerAccountId, draft.OperationId, listing.Id, null, EscrowMovementKind, draft.Now);
        if (escrow is not null && escrow.BidderAccountId == draft.BuyerAccountId)
        {
            escrow.Status = 2;
            escrow.ClosedAt = draft.Now;
            if (!excess.IsEmpty)
            {
                context.Set<AuctionCollection>().Add(MixedCollection(draft.BuyerAccountId, listing.Id, RefundCollectionKind, excess, draft.Now));
            }
        }
        else
        {
            if (escrow is not null)
            {
                escrow.Status = 1;
                escrow.ClosedAt = draft.Now;
                context.Set<AuctionCollection>().Add(MixedCollection(escrow.BidderAccountId, listing.Id, RefundCollectionKind, PriceBasket.Parse(escrow.AmountVector) ?? PriceBasket.Empty, draft.Now));
                context.Set<AuctionNotification>().Add(Notify(escrow.BidderAccountId, listing.Id, OutbidNotificationKind, MixedCurrency, null, draft.Now));
            }

            context.Set<AuctionBid>().Add(new AuctionBid
            {
                Id = Guid.NewGuid(),
                ListingId = listing.Id,
                BidderAccountId = draft.BuyerAccountId,
                BidderCharacterId = draft.BuyerCharacterId,
                Currency = MixedCurrency,
                AmountVector = buyout.ToVectorText(),
                BidderIp = draft.BuyerIp,
                Status = 2,
                CreatedAt = draft.Now,
                ClosedAt = draft.Now,
            });
        }

        listing.Status = 1;
        ConsumeSaleFee(listing);
        listing.CurrentBid = null;
        listing.CurrentBidVector = buyout.ToVectorText();
        listing.CurrentBidderAccountId = draft.BuyerAccountId;
        listing.CurrentBidderCharacterId = draft.BuyerCharacterId;
        listing.FinalizedAt = draft.Now;
        context.Set<AuctionCollection>().Add(ItemCollection(draft.BuyerAccountId, listing, ItemToBuyerCollectionKind, draft.Now));
        context.Set<AuctionCollection>().Add(MixedCollection(listing.SellerAccountId, listing.Id, ProceedsCollectionKind, buyout, draft.Now));
        context.Set<AuctionNotification>().Add(Notify(draft.BuyerAccountId, listing.Id, BoughtNotificationKind, MixedCurrency, null, draft.Now));
        context.Set<AuctionNotification>().Add(Notify(listing.SellerAccountId, listing.Id, SoldNotificationKind, MixedCurrency, null, draft.Now));
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
        context.Set<AuctionTransactionLog>().Add(Audit(BuyoutKind, draft.BuyerAccountId, draft.BuyerCharacterId, listing.Id, draft.OperationId, draft.Now, draft.BuyerIp, listing.SellerAccountId, null, MixedCurrency, null, SameAddress(draft.BuyerIp, listing.SellerIp)));

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

    /// <summary>
    /// Pays a basket from the character: Zen from the money, each jewel kind from the inventory through the payment planner, and the
    /// fruits as exact clean items. The caller's transaction makes it all or nothing: when a component cannot be paid, the caller
    /// rolls back and the components paid before it are given back.
    /// </summary>
    private async Task<bool> TenderBasketAsync(EntityDataContext context, Guid accountId, Guid characterId, Guid operationId, Guid listingId, PriceBasket basket, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var zen = basket.Get(PriceKind.Zen);
        if (zen > 0)
        {
            if (!await DebitZenAsync(context, characterId, zen, cancellationToken))
            {
                return false;
            }

            context.Set<AuctionCurrencyMovement>().Add(Movement(accountId, operationId, listingId, null, zen, TenderInKind, now));
        }

        for (var kind = PriceKind.Chaos; kind <= PriceKind.Harmony; kind++)
        {
            var quantity = basket.Get(kind);
            if (quantity > 0 && await this.TenderJewelsAsync(context, accountId, characterId, operationId, listingId, (byte)kind, quantity, null, now, cancellationToken) != JewelTender.Paid)
            {
                return false;
            }
        }

        var fruits = new FruitBasket(
            basket.Get(PriceKind.StrengthFruit),
            basket.Get(PriceKind.AgilityFruit),
            basket.Get(PriceKind.VitalityFruit),
            basket.Get(PriceKind.EnergyFruit),
            basket.Get(PriceKind.CommandFruit));
        return fruits.Strength + fruits.Agility + fruits.Vitality + fruits.Energy + fruits.Command == 0
            || await this.TenderFruitsAsync(context, accountId, characterId, operationId, listingId, fruits, TenderInKind, null, now, cancellationToken);
    }

    private static byte CurrencyOf(PriceKind kind) => PriceRules.IsFruit(kind) ? FruitsCurrency : (byte)kind;

    private static byte? FruitIndexOf(PriceKind kind) => PriceRules.IsFruit(kind) ? (byte)(kind - PriceKind.StrengthFruit) : null;

    /// <summary>
    /// Records one ledger row per component of a basket, in the currency of the component, so each kind is conserved separately.
    /// </summary>
    private static void AddComponentMovements(EntityDataContext context, PriceBasket basket, Guid accountId, Guid operationId, Guid? listingId, Guid? bidId, byte kind, DateTimeOffset now)
    {
        foreach (var component in basket.Components)
        {
            var movement = Movement(accountId, operationId, listingId, bidId, component.Quantity, kind, now);
            movement.Currency = CurrencyOf(component.Kind);
            movement.FruitComponent = FruitIndexOf(component.Kind);
            context.Set<AuctionCurrencyMovement>().Add(movement);
        }
    }

    /// <summary>
    /// Creates a collection which owes a whole basket.
    /// </summary>
    private static AuctionCollection MixedCollection(Guid accountId, Guid listingId, byte kind, PriceBasket amount, DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ListingId = listingId,
            Kind = kind,
            Currency = MixedCurrency,
            OriginalVector = amount.ToVectorText(),
            RemainingVector = amount.ToVectorText(),
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };

    /// <summary>
    /// Claims a basket collection whole. The Zen is credited and the jewels and fruits are created as items, or nothing changes.
    /// </summary>
    private async Task<AuctionClaimResult> ClaimMixedAsync(EntityDataContext context, ClaimDraft draft, AuctionCollection collection, CancellationToken cancellationToken)
    {
        if (draft.RequestedScalar is not null || draft.RequestedFruits is not null)
        {
            return new AuctionClaimResult(AuctionClaimOutcome.InvalidClaimQuantity, 0, null, 0);
        }

        var remaining = PriceBasket.Parse(collection.RemainingVector) ?? PriceBasket.Empty;
        var now = draft.Now;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var updated = await context.Set<AuctionCollection>()
            .Where(c => c.Id == draft.CollectionId && c.AccountId == draft.AccountId && c.Version == draft.ExpectedVersion && c.Status != 2)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Status, (byte)2)
                .SetProperty(c => c.UpdatedAt, now)
                .SetProperty(c => c.Version, c => c.Version + 1)
                .SetProperty(c => c.RemainingVector, EmptyVector), cancellationToken);
        if (updated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AuctionClaimResult(AuctionClaimOutcome.Stale, 0, null, 0);
        }

        var zen = remaining.Get(PriceKind.Zen);
        if (!remaining.IsEmpty)
        {
            if (!await OwnsCharacterAsync(context, draft.AccountId, draft.CharacterId, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AuctionClaimResult(AuctionClaimOutcome.NotFound, 0, null, 0);
            }

            if (zen > 0 && !await this.CreditZenAsync(context, draft.CharacterId, zen, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AuctionClaimResult(AuctionClaimOutcome.MoneyLimit, 0, null, 0);
            }

            var placed = await this.MaterializeBasketAsync(context, draft.CharacterId, remaining, cancellationToken);
            if (placed != JewelMaterialization.Materialized)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new AuctionClaimResult(placed == JewelMaterialization.InventoryFull ? AuctionClaimOutcome.InventoryFull : AuctionClaimOutcome.UnsupportedCurrency, 0, null, 0);
            }

            foreach (var component in remaining.Components)
            {
                var movement = Movement(draft.AccountId, draft.OperationId, null, null, component.Quantity, ClaimMovementKind, now);
                movement.CollectionId = collection.Id;
                movement.Currency = CurrencyOf(component.Kind);
                movement.FruitComponent = FruitIndexOf(component.Kind);
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
        context.Set<AuctionTransactionLog>().Add(Audit(ClaimKind, draft.AccountId, draft.CharacterId, null, draft.OperationId, now, null, null, zen, MixedCurrency, null));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AuctionClaimResult(AuctionClaimOutcome.Claimed, collection.Kind, null, zen);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return new AuctionClaimResult(AuctionClaimOutcome.Replayed, 0, null, 0);
        }
    }

    /// <summary>
    /// Creates the items of a claimed basket in one pass: the jewels as the largest packs first and then loose jewels, the fruits as
    /// clean fruits at the level of their kind. All items share one search for free main-inventory slots, so two kinds never
    /// take the same slot. Either every item is placed or none is.
    /// </summary>
    private async Task<JewelMaterialization> MaterializeBasketAsync(EntityDataContext context, Guid characterId, PriceBasket basket, CancellationToken cancellationToken)
    {
        var inventoryId = await context.Set<Character>().AsNoTracking()
            .Where(c => c.Id == characterId)
            .Select(c => c.InventoryId)
            .SingleOrDefaultAsync(cancellationToken);
        if (inventoryId is null)
        {
            return JewelMaterialization.InventoryFull;
        }

        var plan = new List<(Guid? Definition, byte Level)>();
        for (var kind = PriceKind.Chaos; kind <= PriceKind.Harmony; kind++)
        {
            var units = basket.Get(kind);
            if (units <= 0)
            {
                continue;
            }

            var (single, pack) = await JewelDefinitionsAsync(context, (byte)kind, cancellationToken);
            if (single is null || pack is null)
            {
                return JewelMaterialization.NotConfigured;
            }

            foreach (byte level in new byte[] { 2, 1, 0 })
            {
                var packUnits = JewelPackRules.GetPackUnits(level);
                while (units >= packUnits)
                {
                    plan.Add((pack, level));
                    units -= packUnits;
                }
            }

            plan.AddRange(Enumerable.Repeat<(Guid? Definition, byte Level)>((single, 0), (int)units));
        }

        if (Enum.GetValues<PriceKind>().Any(k => PriceRules.IsFruit(k) && basket.Get(k) > 0))
        {
            var fruit = await context.Database.SqlQuery<Guid?>(
                $"SELECT d.\"Id\" AS \"Value\" FROM config.\"ItemDefinition\" d WHERE d.\"Group\" = 13 AND d.\"Number\" = 15")
                .SingleOrDefaultAsync(cancellationToken);
            if (fruit is null)
            {
                return JewelMaterialization.NotConfigured;
            }

            foreach (var component in Enum.GetValues<FruitComponent>())
            {
                var count = basket.Get(PriceKind.StrengthFruit + (int)component);
                plan.AddRange(Enumerable.Repeat<(Guid? Definition, byte Level)>((fruit, (byte)FruitRules.GetItemLevel(component)), (int)count));
            }
        }

        var freeSlots = await context.Database.SqlQuery<int>(
            $"SELECT s.n AS \"Value\" FROM generate_series({InventoryConstants.EquippableSlotsCount}, {InventoryConstants.EquippableSlotsCount + 63}) AS s(n) WHERE s.n NOT IN (SELECT i.\"ItemSlot\" FROM data.\"Item\" i WHERE i.\"ItemStorageId\" = {inventoryId}) ORDER BY s.n")
            .ToListAsync(cancellationToken);
        if (freeSlots.Count < plan.Count)
        {
            return JewelMaterialization.InventoryFull;
        }

        for (var index = 0; index < plan.Count; index++)
        {
            context.Set<Item>().Add(new Item
            {
                Id = Guid.NewGuid(),
                ItemStorageId = inventoryId,
                ItemSlot = (byte)freeSlots[index],
                DefinitionId = plan[index].Definition,
                Level = plan[index].Level,
                Durability = 1,
            });
        }

        return JewelMaterialization.Materialized;
    }
}
