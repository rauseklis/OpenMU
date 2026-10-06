// <copyright file="AuctionListingCreationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies listing creation against PostgreSQL: the listing and its operation record commit together, a replayed
/// operation returns the stored outcome without creating anything, and an already-listed item is refused.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionListingCreationTests
{
    private Guid _accountId;

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
    public void Arrange()
    {
        this._accountId = Guid.NewGuid();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionOperation>().Where(operation => operation.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionListing>().Where(listing => listing.SellerAccountId == this._accountId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies a new listing is created together with its operation record.
    /// </summary>
    [Test]
    public async Task CreatesListingAndOperationTogether()
    {
        var repository = new AuctionHouseRepository();
        await using var context = new EntityDataContext();
        var operationId = Guid.NewGuid();

        var result = await repository.CreateListingAsync(context, this.Draft(Guid.NewGuid(), operationId));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AuctionCreateOutcome.Created));
            Assert.That(result.ListingNumber, Is.GreaterThan(0));
        });
        var operation = await context.Set<AuctionOperation>().AsNoTracking().SingleAsync(o => o.AccountId == this._accountId && o.OperationId == operationId);
        Assert.That(operation.ListingId, Is.EqualTo(result.ListingId));
    }

    /// <summary>
    /// Verifies replaying the same operation returns the original outcome and creates nothing further.
    /// </summary>
    [Test]
    public async Task ReplayReturnsTheStoredOutcomeWithoutCreatingAgain()
    {
        var repository = new AuctionHouseRepository();
        await using var context = new EntityDataContext();
        var operationId = Guid.NewGuid();
        var first = await repository.CreateListingAsync(context, this.Draft(Guid.NewGuid(), operationId));

        var replay = await repository.CreateListingAsync(context, this.Draft(Guid.NewGuid(), operationId));

        Assert.Multiple(() =>
        {
            Assert.That(replay.Outcome, Is.EqualTo(AuctionCreateOutcome.Replayed));
            Assert.That(replay.ListingNumber, Is.EqualTo(first.ListingNumber));
            Assert.That(context.Set<AuctionListing>().Count(l => l.SellerAccountId == this._accountId), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies an item which is already actively listed is refused, and that no operation record is left behind.
    /// </summary>
    [Test]
    public async Task AlreadyListedItemIsRefusedWithoutOperationRecord()
    {
        var repository = new AuctionHouseRepository();
        await using var context = new EntityDataContext();
        var itemId = Guid.NewGuid();
        await repository.CreateListingAsync(context, this.Draft(itemId, Guid.NewGuid()));
        var secondOperation = Guid.NewGuid();

        var result = await repository.CreateListingAsync(context, this.Draft(itemId, secondOperation));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AuctionCreateOutcome.ItemAlreadyListed));
            Assert.That(context.Set<AuctionOperation>().Any(o => o.OperationId == secondOperation), Is.False);
        });
    }

    private AuctionListingDraft Draft(Guid itemId, Guid operationId) => new(
        OperationId: operationId,
        AccountId: this._accountId,
        ItemId: itemId,
        SellerCharacterId: Guid.NewGuid(),
        SellerName: "Seller",
        SellerIp: "127.0.0.1",
        Currency: 0,
        Category: 0,
        StartingPrice: 1000,
        CreatedAt: DateTimeOffset.UtcNow,
        EndsAt: DateTimeOffset.UtcNow.AddDays(1));
}
