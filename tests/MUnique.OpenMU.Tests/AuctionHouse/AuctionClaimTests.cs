// <copyright file="AuctionClaimTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies collection claims against PostgreSQL: only the owning account can claim, a claim happens once, a stale or
/// repeated claim changes nothing, and two racing claims cannot both succeed.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionClaimTests
{
    private const long RefundAmount = 1000;
    private Guid _characterId;
    private Guid _accountId;
    private Guid _otherAccountId;
    private Guid _itemId;
    private Guid _lotCollectionId;
    private Guid _refundCollectionId;

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
        var funded = await AuctionTestCharacters.FundAsync(AuctionPostgresTestEnvironment.ConnectionString!, 0);
        if (funded is null)
        {
            Assert.Ignore("The database has no characters to credit; use the populated copy.");
        }

        this._characterId = funded.Value;
        this._accountId = Guid.NewGuid();
        this._otherAccountId = Guid.NewGuid();
        this._itemId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var context = new EntityDataContext();
        var lot = Collection(this._accountId, now, kind: 0, itemId: this._itemId, scalar: null);
        var refund = Collection(this._accountId, now, kind: 3, itemId: null, scalar: RefundAmount);
        this._lotCollectionId = lot.Id;
        this._refundCollectionId = refund.Id;
        context.Set<AuctionCollection>().AddRange(lot, refund);
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => c.AccountId == this._accountId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies the owner can claim an item collection, and the claim returns the item to deliver.
    /// </summary>
    [Test]
    public async Task OwnerClaimsItemCollection()
    {
        var result = await this.Claim(this._accountId, this._lotCollectionId, 0, Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
            Assert.That(result.ItemId, Is.EqualTo(this._itemId));
        });
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionCollection>().AsNoTracking().Where(c => c.Id == this._lotCollectionId).Select(c => c.Status).SingleAsync(), Is.EqualTo(2));
    }

    /// <summary>
    /// Verifies the owner can claim a scalar collection, and the claim returns the full amount.
    /// </summary>
    [Test]
    public async Task OwnerClaimsScalarCollectionInFull()
    {
        var result = await this.Claim(this._accountId, this._refundCollectionId, 0, Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
            Assert.That(result.Amount, Is.EqualTo(RefundAmount));
        });
    }

    /// <summary>
    /// Verifies another account cannot claim a collection, and the collection is left untouched.
    /// </summary>
    [Test]
    public async Task OtherAccountCannotClaim()
    {
        var result = await this.Claim(this._otherAccountId, this._refundCollectionId, 0, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.NotFound));
        await using var context = new EntityDataContext();
        Assert.That(await context.Set<AuctionCollection>().AsNoTracking().Where(c => c.Id == this._refundCollectionId).Select(c => c.Status).SingleAsync(), Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies a claim against an outdated version changes nothing.
    /// </summary>
    [Test]
    public async Task StaleVersionIsRefused()
    {
        var result = await this.Claim(this._accountId, this._refundCollectionId, 7, Guid.NewGuid());

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Stale));
    }

    /// <summary>
    /// Verifies a collection cannot be claimed twice.
    /// </summary>
    [Test]
    public async Task CollectionCannotBeClaimedTwice()
    {
        await this.Claim(this._accountId, this._refundCollectionId, 0, Guid.NewGuid());

        var second = await this.Claim(this._accountId, this._refundCollectionId, 1, Guid.NewGuid());

        Assert.That(second.Outcome, Is.EqualTo(AuctionClaimOutcome.AlreadyClaimed));
    }

    /// <summary>
    /// Verifies a replayed claim returns the stored outcome and does not claim again.
    /// </summary>
    [Test]
    public async Task ReplayedClaimChangesNothing()
    {
        var operation = Guid.NewGuid();
        await this.Claim(this._accountId, this._refundCollectionId, 0, operation);

        var replay = await this.Claim(this._accountId, this._refundCollectionId, 0, operation);

        Assert.That(replay.Outcome, Is.EqualTo(AuctionClaimOutcome.Replayed));
    }

    /// <summary>
    /// Verifies two racing claims on the same version cannot both succeed.
    /// </summary>
    [Test]
    public async Task RacingClaimsCannotBothSucceed()
    {
        var first = this.Claim(this._accountId, this._refundCollectionId, 0, Guid.NewGuid());
        var second = this.Claim(this._accountId, this._refundCollectionId, 0, Guid.NewGuid());

        await Task.WhenAll(first, second);

        var outcomes = new[] { first.Result.Outcome, second.Result.Outcome };
        Assert.Multiple(() =>
        {
            Assert.That(outcomes.Count(o => o == AuctionClaimOutcome.Claimed), Is.EqualTo(1));
            Assert.That(outcomes.Count(o => o == AuctionClaimOutcome.Stale), Is.EqualTo(1));
        });
    }

    private static AuctionCollection Collection(Guid account, DateTimeOffset now, byte kind, Guid? itemId, long? scalar)
    {
        return new AuctionCollection
        {
            Id = Guid.NewGuid(),
            AccountId = account,
            Kind = kind,
            Currency = 0,
            ItemId = itemId,
            OriginalScalar = scalar,
            RemainingScalar = scalar,
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };
    }

    private Task<AuctionClaimResult> Claim(Guid account, Guid collectionId, int expectedVersion, Guid operationId)
    {
        return new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(
            OperationId: operationId,
            AccountId: account,
            CharacterId: this._characterId,
            CollectionId: collectionId,
            ExpectedVersion: expectedVersion,
            Now: DateTimeOffset.UtcNow));
    }
}
