// <copyright file="RateLimiter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Security;

/// <summary>
/// A simple in-memory sliding window rate limiter, keyed by text (for example "ip:1.2.3.4" or "acct:name").
/// </summary>
public sealed class RateLimiter
{
    private readonly Dictionary<string, Queue<DateTimeOffset>> _hits = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private readonly Func<DateTimeOffset> _clock;
    private DateTimeOffset _nextCleanup = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimiter"/> class.
    /// </summary>
    /// <param name="clock">The clock; defaults to the system clock.</param>
    public RateLimiter(Func<DateTimeOffset>? clock = null)
    {
        this._clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Counts a hit and tells whether it is still within the limit.
    /// </summary>
    /// <param name="key">The key, including the kind of limit.</param>
    /// <param name="limit">The number of hits allowed within the window.</param>
    /// <param name="window">The length of the window.</param>
    /// <returns><c>true</c> if the hit is allowed; <c>false</c> if the limit is exceeded (the hit is then not counted).</returns>
    public bool TryHit(string key, int limit, TimeSpan window)
    {
        var now = this._clock();
        lock (this._sync)
        {
            this.Cleanup(now);
            if (!this._hits.TryGetValue(key, out var queue))
            {
                queue = new Queue<DateTimeOffset>();
                this._hits[key] = queue;
            }

            while (queue.Count > 0 && now - queue.Peek() >= window)
            {
                queue.Dequeue();
            }

            if (queue.Count >= limit)
            {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    /// Forgets the hits of the key, for example after a successful login.
    /// </summary>
    /// <param name="key">The key.</param>
    public void Reset(string key)
    {
        lock (this._sync)
        {
            this._hits.Remove(key);
        }
    }

    private void Cleanup(DateTimeOffset now)
    {
        if (now < this._nextCleanup)
        {
            return;
        }

        this._nextCleanup = now.AddMinutes(1);

        // The longest window in use is one hour.
        var longest = TimeSpan.FromHours(1);
        foreach (var key in this._hits.Where(p => p.Value.Count == 0 || now - p.Value.Last() >= longest).Select(p => p.Key).ToList())
        {
            this._hits.Remove(key);
        }
    }
}
