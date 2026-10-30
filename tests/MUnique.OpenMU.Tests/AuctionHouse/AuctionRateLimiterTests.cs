// <copyright file="AuctionRateLimiterTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the per-account request budgets: a burst is allowed, then the budget refills at its rate, and accounts are independent.
/// </summary>
[TestFixture]
public class AuctionRateLimiterTests
{
    /// <summary>
    /// Verifies a create request beyond its burst of two is refused until the budget refills.
    /// </summary>
    [Test]
    public void CreateBudgetAllowsTheBurstThenRefills()
    {
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var limiter = new AuctionRateLimiter(() => now);
        var account = Guid.NewGuid();

        var first = limiter.TryAcquire(account, AuctionRequestCost.Create);
        var second = limiter.TryAcquire(account, AuctionRequestCost.Create);
        var third = limiter.TryAcquire(account, AuctionRequestCost.Create);
        now = now.AddSeconds(5);
        var afterRefill = limiter.TryAcquire(account, AuctionRequestCost.Create);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(third, Is.False);
            Assert.That(afterRefill, Is.True);
        });
    }

    /// <summary>
    /// Verifies one account's budget does not affect another account.
    /// </summary>
    [Test]
    public void AccountsHaveIndependentBudgets()
    {
        var limiter = new AuctionRateLimiter(() => DateTimeOffset.UtcNow);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            limiter.TryAcquire(first, AuctionRequestCost.Mutation);
        }

        Assert.That(limiter.TryAcquire(second, AuctionRequestCost.Mutation), Is.True);
    }

    /// <summary>
    /// Verifies a query budget allows a burst of ten requests, and the eleventh is refused.
    /// </summary>
    [Test]
    public void QueryBudgetAllowsTenInABurst()
    {
        var limiter = new AuctionRateLimiter(() => new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var account = Guid.NewGuid();

        var allowed = Enumerable.Range(0, 11).Count(_ => limiter.TryAcquire(account, AuctionRequestCost.Query));

        Assert.That(allowed, Is.EqualTo(10));
    }

    /// <summary>
    /// Verifies that dropping refilled buckets of many other accounts never gives an exhausted account a fresh budget.
    /// </summary>
    [Test]
    public void PruningManyAccountsKeepsAnExhaustedBudget()
    {
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var limiter = new AuctionRateLimiter(() => now);
        var busy = Guid.NewGuid();
        Assert.That(limiter.TryAcquire(busy, AuctionRequestCost.Create), Is.True);
        Assert.That(limiter.TryAcquire(busy, AuctionRequestCost.Create), Is.True);
        Assert.That(limiter.TryAcquire(busy, AuctionRequestCost.Create), Is.False);

        for (var i = 0; i < 5000; i++)
        {
            limiter.TryAcquire(Guid.NewGuid(), AuctionRequestCost.Create);
        }

        Assert.That(limiter.TryAcquire(busy, AuctionRequestCost.Create), Is.False);

        now = now.AddHours(1);
        Assert.That(limiter.TryAcquire(busy, AuctionRequestCost.Create), Is.True);
    }
}
