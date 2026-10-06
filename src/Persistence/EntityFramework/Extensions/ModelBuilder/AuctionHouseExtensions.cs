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
}
