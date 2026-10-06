// <copyright file="AuctionListingPersistenceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using Npgsql;

/// <summary>
/// Verifies the AuctionListing entity against a real PostgreSQL database: identity, round trip and the single-active-listing rule.
/// Each test runs in a transaction that is rolled back, so the database is left unchanged.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionListingPersistenceTests
{
    [OneTimeSetUp]
    public void SetUp()
    {
        if (AuctionPostgresTestEnvironment.ConnectionString is null)
        {
            Assert.Ignore($"Set {AuctionPostgresTestEnvironment.ConnectionStringVariable} to run the PostgreSQL integration tests.");
        }

        AuctionPostgresTestEnvironment.EnsureConfigured();
    }

    /// <summary>
    /// Verifies a listing round-trips, and receives a database-generated listing number.
    /// </summary>
    [Test]
    public async Task ListingRoundTripsWithDatabaseListingNumber()
    {
        await using var context = new EntityDataContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var listing = NewListing(Guid.NewGuid());
        var set = context.Set<AuctionListing>();

        set.Add(listing);
        await context.SaveChangesAsync();

        Assert.That(listing.ListingNumber, Is.GreaterThan(0));
        var loaded = await set.AsNoTracking().SingleAsync(l => l.Id == listing.Id);
        Assert.Multiple(() =>
        {
            Assert.That(loaded.ListingNumber, Is.EqualTo(listing.ListingNumber));
            Assert.That(loaded.StartingPrice, Is.EqualTo(listing.StartingPrice));
            Assert.That(loaded.EndsAt, Is.EqualTo(listing.EndsAt));
        });

        await transaction.RollbackAsync();
    }

    /// <summary>
    /// Verifies a second active listing for the same item is rejected by the database.
    /// </summary>
    [Test]
    public async Task SecondActiveListingForTheSameItemIsRejected()
    {
        await using var context = new EntityDataContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var itemId = Guid.NewGuid();
        var set = context.Set<AuctionListing>();
        set.Add(NewListing(itemId));
        await context.SaveChangesAsync();

        set.Add(NewListing(itemId));
        var exception = Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.That(((PostgresException)exception!.InnerException!).SqlState, Is.EqualTo(PostgresErrorCodes.UniqueViolation));
        await transaction.RollbackAsync();
    }

    private static AuctionListing NewListing(Guid itemId)
    {
        // PostgreSQL stores timestamps to the microsecond, so the test truncates to the same precision.
        var now = new DateTimeOffset(DateTimeOffset.UtcNow.Ticks / 10 * 10, TimeSpan.Zero);
        return new AuctionListing
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            SellerAccountId = Guid.NewGuid(),
            SellerCharacterId = Guid.NewGuid(),
            SellerName = "Seller",
            Status = 0,
            Currency = 0,
            Category = 0,
            StartingPrice = 1000,
            CreatedAt = now,
            OriginalEndsAt = now.AddDays(1),
            EndsAt = now.AddDays(1),
            NormalizedName = "item",
        };
    }
}
