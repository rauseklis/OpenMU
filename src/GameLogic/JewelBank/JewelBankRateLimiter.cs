// <copyright file="JewelBankRateLimiter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.JewelBank;

using System.Collections.Concurrent;

/// <summary>
/// Limits how fast one account may send mutating Jewel Bank requests: a request is refused when the previous
/// one of the same account was less than the minimum interval ago.
/// </summary>
public sealed class JewelBankRateLimiter
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _interval;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _last = new();
    private readonly object _sync = new();

    /// <summary>Above this many remembered accounts, entries which can no longer limit anything are dropped.</summary>
    private const int PruneThreshold = 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="JewelBankRateLimiter"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="interval">The minimum interval between two requests of an account.</param>
    public JewelBankRateLimiter(Func<DateTimeOffset> clock, TimeSpan interval)
    {
        this._clock = clock;
        this._interval = interval;
    }

    /// <summary>
    /// Registers a request of the account.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <returns><c>true</c> when the request may proceed.</returns>
    public bool TryAcquire(Guid accountId)
    {
        var now = this._clock();
        lock (this._sync)
        {
            if (this._last.TryGetValue(accountId, out var previous) && now - previous < this._interval)
            {
                return false;
            }

            if (this._last.Count >= PruneThreshold)
            {
                // An entry older than the interval behaves exactly like no entry, so removing it changes nothing but the memory use.
                foreach (var entry in this._last)
                {
                    if (now - entry.Value >= this._interval)
                    {
                        this._last.TryRemove(entry.Key, out _);
                    }
                }
            }

            this._last[accountId] = now;
            return true;
        }
    }
}
