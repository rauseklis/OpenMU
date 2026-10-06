// <copyright file="AuctionRateLimiter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Collections.Concurrent;

/// <summary>
/// The cost class of a request. Each class has its own budget per account.
/// </summary>
public enum AuctionRequestCost
{
    /// <summary>Open, browse, detail, my listings and mailbox.</summary>
    Query,

    /// <summary>Bid and buyout.</summary>
    Mutation,

    /// <summary>Create, cancel and collect.</summary>
    Create,
}

/// <summary>
/// Token buckets per account and cost class. A request takes one token; a bucket refills at its rate up to its burst.
/// </summary>
public sealed class AuctionRateLimiter
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly ConcurrentDictionary<(Guid AccountId, AuctionRequestCost Cost), Bucket> _buckets = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionRateLimiter"/> class.
    /// </summary>
    /// <param name="clock">The clock which times the refill.</param>
    public AuctionRateLimiter(Func<DateTimeOffset> clock)
    {
        this._clock = clock;
    }

    /// <summary>
    /// Takes one token for the request, if the account has one left.
    /// </summary>
    /// <param name="accountId">The requesting account.</param>
    /// <param name="cost">The cost class of the request.</param>
    /// <returns><c>true</c> when the request may proceed; otherwise <c>false</c>.</returns>
    public bool TryAcquire(Guid accountId, AuctionRequestCost cost)
    {
        var (ratePerSecond, burst) = Limits(cost);
        var now = this._clock();
        var bucket = this._buckets.GetOrAdd((accountId, cost), _ => new Bucket(burst, now));
        lock (bucket)
        {
            var elapsed = Math.Max(0, (now - bucket.Updated).TotalSeconds);
            bucket.Tokens = Math.Min(burst, bucket.Tokens + (elapsed * ratePerSecond));
            bucket.Updated = now;
            if (bucket.Tokens < 1)
            {
                return false;
            }

            bucket.Tokens -= 1;
            return true;
        }
    }

    private static (double RatePerSecond, double Burst) Limits(AuctionRequestCost cost)
        => cost switch
        {
            AuctionRequestCost.Query => (5.0, 10.0),
            AuctionRequestCost.Mutation => (1.0, 3.0),
            _ => (0.2, 2.0),
        };

    private sealed class Bucket
    {
        public Bucket(double tokens, DateTimeOffset updated)
        {
            this.Tokens = tokens;
            this.Updated = updated;
        }

        public double Tokens { get; set; }

        public DateTimeOffset Updated { get; set; }
    }
}
