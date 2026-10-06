// <copyright file="AuctionPartialClaimTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies partial claims against PostgreSQL: a jewel collection can be claimed in part, a Zen collection only in full,
/// and a fruit-basket collection component by component, never beyond what remains.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionPartialClaimTests
{
    private Guid _accountId;
    private Guid _characterId;
    private Guid _jewelId;
    private Guid _zenId;
    private Guid _fruitId;

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
        var now = DateTimeOffset.UtcNow;
        var jewel = Scalar(this._accountId, now, kind: 3, currency: 1, amount: 30);
        var zen = Scalar(this._accountId, now, kind: 3, currency: 0, amount: 1000);
        var fruit = Fruit(this._accountId, now);
        this._jewelId = jewel.Id;
        this._zenId = zen.Id;
        this._fruitId = fruit.Id;
        await using var context = new EntityDataContext();
        context.Set<AuctionCollection>().AddRange(jewel, zen, fruit);
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionCurrencyMovement>().Where(m => m.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionOperation>().Where(o => o.AccountId == this._accountId).ExecuteDeleteAsync();
        await context.Set<AuctionCollection>().Where(c => c.AccountId == this._accountId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies a part of a jewel collection can be claimed, leaving the rest pending.
    /// </summary>
    [Test]
    public async Task JewelCollectionCanBeClaimedInPart()
    {
        var result = await this.Claim(this._jewelId, 0, Guid.NewGuid(), scalar: 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
            Assert.That(result.Amount, Is.EqualTo(10));
        });
        await using var context = new EntityDataContext();
        var collection = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.Id == this._jewelId);
        Assert.Multiple(() =>
        {
            Assert.That(collection.RemainingScalar, Is.EqualTo(20));
            Assert.That(collection.Status, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies claiming the rest of a partly claimed jewel collection completes it.
    /// </summary>
    [Test]
    public async Task RemainderOfAJewelCollectionCompletesIt()
    {
        await this.Claim(this._jewelId, 0, Guid.NewGuid(), scalar: 10);

        var result = await this.Claim(this._jewelId, 1, Guid.NewGuid(), scalar: 20);

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
        await using var context = new EntityDataContext();
        var collection = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.Id == this._jewelId);
        Assert.Multiple(() =>
        {
            Assert.That(collection.RemainingScalar, Is.EqualTo(0));
            Assert.That(collection.Status, Is.EqualTo(2));
        });
    }

    /// <summary>
    /// Verifies a jewel claim larger than what remains is refused.
    /// </summary>
    [Test]
    public async Task JewelClaimLargerThanRemainingIsRefused()
    {
        var result = await this.Claim(this._jewelId, 0, Guid.NewGuid(), scalar: 31);

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.InvalidClaimQuantity));
    }

    /// <summary>
    /// Verifies a Zen collection cannot be claimed in part.
    /// </summary>
    [Test]
    public async Task ZenCollectionCannotBeClaimedInPart()
    {
        var result = await this.Claim(this._zenId, 0, Guid.NewGuid(), scalar: 500);

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.InvalidClaimQuantity));
    }

    /// <summary>
    /// Verifies a fruit basket can be claimed component by component, and that the remaining basket is reduced by exactly
    /// the requested quantities.
    /// </summary>
    [Test]
    public async Task FruitBasketCanBeClaimedPerComponent()
    {
        var result = await this.Claim(this._fruitId, 0, Guid.NewGuid(), fruits: new FruitQuantities(4, 0, 0, 0, 2));

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.Claimed));
        await using var context = new EntityDataContext();
        var collection = await context.Set<AuctionCollection>().AsNoTracking().SingleAsync(c => c.Id == this._fruitId);
        Assert.Multiple(() =>
        {
            Assert.That(collection.RemainingStrength, Is.EqualTo(6));
            Assert.That(collection.RemainingAgility, Is.EqualTo(5));
            Assert.That(collection.RemainingCommand, Is.EqualTo(0));
            Assert.That(collection.Status, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies a fruit claim that exceeds one component's remaining quantity is refused without changes.
    /// </summary>
    [Test]
    public async Task FruitClaimOverAComponentIsRefused()
    {
        var result = await this.Claim(this._fruitId, 0, Guid.NewGuid(), fruits: new FruitQuantities(11, 0, 0, 0, 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.InvalidClaimQuantity));
    }

    /// <summary>
    /// Verifies a fruit claim that requests no fruit at all is refused.
    /// </summary>
    [Test]
    public async Task FruitClaimWithNothingRequestedIsRefused()
    {
        var result = await this.Claim(this._fruitId, 0, Guid.NewGuid(), fruits: new FruitQuantities(0, 0, 0, 0, 0));

        Assert.That(result.Outcome, Is.EqualTo(AuctionClaimOutcome.InvalidClaimQuantity));
    }

    private static AuctionCollection Scalar(Guid account, DateTimeOffset now, byte kind, byte currency, long amount)
    {
        return new AuctionCollection
        {
            Id = Guid.NewGuid(),
            AccountId = account,
            Kind = kind,
            Currency = currency,
            OriginalScalar = amount,
            RemainingScalar = amount,
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };
    }

    private static AuctionCollection Fruit(Guid account, DateTimeOffset now)
    {
        return new AuctionCollection
        {
            Id = Guid.NewGuid(),
            AccountId = account,
            Kind = 2,
            Currency = 8,
            OriginalStrength = 10,
            OriginalAgility = 5,
            OriginalVitality = 0,
            OriginalEnergy = 20,
            OriginalCommand = 2,
            RemainingStrength = 10,
            RemainingAgility = 5,
            RemainingVitality = 0,
            RemainingEnergy = 20,
            RemainingCommand = 2,
            Status = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0,
        };
    }

    private Task<AuctionClaimResult> Claim(Guid collectionId, int expectedVersion, Guid operationId, long? scalar = null, FruitQuantities? fruits = null)
    {
        return new AuctionHouseRepository().ClaimAsync(new EntityDataContext(), new ClaimDraft(
            OperationId: operationId,
            AccountId: this._accountId,
            CharacterId: this._characterId,
            CollectionId: collectionId,
            ExpectedVersion: expectedVersion,
            Now: DateTimeOffset.UtcNow,
            RequestedScalar: scalar,
            RequestedFruits: fruits));
    }
}
