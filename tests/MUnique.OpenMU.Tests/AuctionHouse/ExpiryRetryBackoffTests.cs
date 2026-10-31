// <copyright file="ExpiryRetryBackoffTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

/// <summary>
/// Verifies that listings which failed to finalize are skipped for a while and then tried again.
/// </summary>
[TestFixture]
public class ExpiryRetryBackoffTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A listing which just failed is skipped.
    /// </summary>
    [Test]
    public void AFailedListingIsSkipped()
    {
        var backoff = new ExpiryRetryBackoff(TimeSpan.FromMinutes(10));
        var failed = Guid.NewGuid();
        backoff.RecordFailure(failed, Start);

        Assert.That(backoff.GetSkippedIds(Start.AddMinutes(1)), Is.EquivalentTo(new[] { failed }));
    }

    /// <summary>
    /// After the delay the listing is tried again and forgotten.
    /// </summary>
    [Test]
    public void AFailedListingIsTriedAgainAfterTheDelay()
    {
        var backoff = new ExpiryRetryBackoff(TimeSpan.FromMinutes(10));
        backoff.RecordFailure(Guid.NewGuid(), Start);

        Assert.That(backoff.GetSkippedIds(Start.AddMinutes(10)), Is.Empty);
        Assert.That(backoff.GetSkippedIds(Start.AddMinutes(1)), Is.Empty);
    }

    /// <summary>
    /// A listing which was finalized later is no longer skipped.
    /// </summary>
    [Test]
    public void AFinalizedListingIsForgotten()
    {
        var backoff = new ExpiryRetryBackoff(TimeSpan.FromMinutes(10));
        var listing = Guid.NewGuid();
        backoff.RecordFailure(listing, Start);
        backoff.RecordSuccess(listing);

        Assert.That(backoff.GetSkippedIds(Start.AddMinutes(1)), Is.Empty);
    }

    /// <summary>
    /// Only the failed listings are skipped; others are not affected.
    /// </summary>
    [Test]
    public void OnlyFailedListingsAreSkipped()
    {
        var backoff = new ExpiryRetryBackoff(TimeSpan.FromMinutes(10));
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        backoff.RecordFailure(first, Start);
        backoff.RecordFailure(second, Start.AddMinutes(8));

        Assert.That(backoff.GetSkippedIds(Start.AddMinutes(11)), Is.EquivalentTo(new[] { second }));
    }
}
