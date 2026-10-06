// <copyright file="AuctionOperationRetentionTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies operation retention against PostgreSQL: an old record of a finished listing is purged, while an old record of
/// an active listing is kept, because its replay is still able to change the state.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionOperationRetentionTests
{
    private Guid _accountId;
    private Guid _finishedListing;
    private Guid _activeListing;
    private Guid _finishedOperation;
    private Guid _activeOperation;

    [OneTimeSetUp]
    public void SetUp()
    {
        if (AuctionPostgresTestEnvironment.ConnectionString is null)
        {
            Assert.Ignore($"Set {AuctionPostgresTestEnvironment.ConnectionStringVariable} to run the PostgreSQL integration tests.");
        }

        AuctionPostgresTestEnvironment.EnsureConfigured();
    }

    [SetUp]
    public async Task Arrange()
    {
        this._accountId = Guid.NewGuid();
        this._finishedListing = Guid.NewGuid();
        this._activeListing = Guid.NewGuid();
        this._finishedOperation = Guid.NewGuid();
        this._activeOperation = Guid.NewGuid();
        var old = DateTimeOffset.UtcNow.AddDays(-30);
        await using var context = new EntityDataContext();
        context.Set<AuctionListing>().AddRange(
            Listing(this._finishedListing, status: 1, old),
            Listing(this._activeListing, status: 0, old));
        context.Set<AuctionOperation>().AddRange(
            Operation(this._accountId, this._finishedOperation, this._finishedListing, old),
            Operation(this._accountId, this._activeOperation, this._activeListing, old));
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(l => l.Id == this._finishedListing || l.Id == this._activeListing).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies an old record of a finished listing is purged, and an old record of an active listing is kept.
    /// </summary>
    [Test]
    public async Task FinishedOperationIsPurgedAndActiveOperationIsKept()
    {
        var deleted = await new AuctionHouseRepository().PurgeFinishedOperationsAsync(DateTimeOffset.UtcNow.AddDays(-7), 5000);

        Assert.That(deleted, Is.GreaterThanOrEqualTo(1));
        await using var context = new EntityDataContext();
        var remaining = await context.Set<AuctionOperation>().AsNoTracking().Where(o => o.AccountId == this._accountId).Select(o => o.OperationId).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(remaining, Does.Not.Contain(this._finishedOperation));
            Assert.That(remaining, Does.Contain(this._activeOperation));
        });
    }

    private static AuctionListing Listing(Guid id, byte status, DateTimeOffset created)
        => new()
        {
            Id = id,
            ItemId = Guid.NewGuid(),
            SellerAccountId = Guid.NewGuid(),
            SellerCharacterId = Guid.NewGuid(),
            SellerName = "Seller",
            Status = status,
            Currency = 0,
            Category = 0,
            StartingPrice = 1000,
            CreatedAt = created,
            OriginalEndsAt = created,
            EndsAt = created,
            NormalizedName = string.Empty,
        };

    private static AuctionOperation Operation(Guid account, Guid operationId, Guid listing, DateTimeOffset created)
        => new()
        {
            Id = Guid.NewGuid(),
            AccountId = account,
            OperationId = operationId,
            Kind = 0,
            Outcome = 0,
            ListingId = listing,
            CreatedAt = created,
        };
}
