// <copyright file="AuctionHouseExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Extensions.ModelBuilder;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Extensions for Auction House <see cref="EntityTypeBuilder"/>s.
/// </summary>
internal static class AuctionHouseExtensions
{
    /// <summary>
    /// Applies the settings for the <see cref="AuctionListing"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionListing> builder)
    {
        builder.Property(listing => listing.ListingNumber).ValueGeneratedOnAdd();
        builder.HasIndex(listing => listing.ListingNumber).IsUnique();

        // An item can be listed only once at a time, but a returned item can be listed again later.
        builder.HasIndex(listing => listing.ItemId)
            .IsUnique()
            .HasFilter("\"Status\" = 0");

        builder.HasIndex(listing => new { listing.Status, listing.Currency, listing.Category, listing.EndsAt, listing.ListingNumber });
        builder.HasIndex(listing => new { listing.SellerAccountId, listing.Status });
        builder.HasIndex(listing => new { listing.CurrentBidderAccountId, listing.Status });
    }

    /// <summary>
    /// Applies the settings for the <see cref="AuctionBid"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionBid> builder)
    {
        // At most one escrowed bid is held per listing.
        builder.HasIndex(bid => bid.ListingId)
            .IsUnique()
            .HasFilter("\"Status\" = 0");
        builder.HasIndex(bid => new { bid.BidderAccountId, bid.Status });
    }

    /// <summary>
    /// Applies the settings for the <see cref="AuctionCollection"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionCollection> builder)
    {
        builder.Property(collection => collection.CollectionNumber).ValueGeneratedOnAdd();
        builder.HasIndex(collection => collection.CollectionNumber).IsUnique();

        // A pending lot item can appear in only one collection.
        builder.HasIndex(collection => collection.ItemId)
            .IsUnique()
            .HasFilter("\"ItemId\" IS NOT NULL");
        builder.HasIndex(collection => new { collection.AccountId, collection.Status });
    }

    /// <summary>
    /// Applies the settings for the <see cref="AuctionCurrencyMovement"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionCurrencyMovement> builder)
    {
        builder.HasIndex(movement => new { movement.AccountId, movement.Currency });
        builder.HasIndex(movement => movement.OperationId);
    }

    /// <summary>
    /// Applies the settings for the <see cref="AuctionOperation"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionOperation> builder)
    {
        // A replayed operation from the same account is recognized by this unique pair.
        builder.HasIndex(operation => new { operation.AccountId, operation.OperationId }).IsUnique();
    }

    /// <summary>
    /// Applies the settings for the <see cref="AuctionTransactionLog"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionTransactionLog> builder)
    {
        builder.HasIndex(log => new { log.ActorAccountId, log.CreatedAt });
        builder.HasIndex(log => log.ListingId);
    }

    /// <summary>
    /// Applies the settings for the <see cref="AuctionAccountRestriction"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionAccountRestriction> builder)
    {
        builder.HasIndex(restriction => restriction.AccountId).IsUnique();
    }

    /// <summary>
    /// Applies the settings for the <see cref="AuctionNotification"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AuctionNotification> builder)
    {
        builder.HasIndex(notification => new { notification.AccountId, notification.DeliveredAt });
    }
}
