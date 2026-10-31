// <copyright file="ExpiryRetryBackoff.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

using System.Collections.Concurrent;

/// <summary>
/// Remembers, in memory only, which ended listings could not be finalized so that the expiry worker leaves them alone for a
/// while. Without it, listings which fail every time would keep taking the places in each batch, ahead of newer ended listings.
/// Nothing is stored: after a restart every listing is tried again.
/// </summary>
public sealed class ExpiryRetryBackoff
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _retryAfter = new();
    private readonly TimeSpan _delay;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiryRetryBackoff"/> class.
    /// </summary>
    /// <param name="delay">How long a listing which failed is left alone.</param>
    public ExpiryRetryBackoff(TimeSpan delay)
    {
        this._delay = delay;
    }

    /// <summary>
    /// Notes that finalizing the listing failed; it is skipped until the delay has passed.
    /// </summary>
    /// <param name="listingId">The listing id.</param>
    /// <param name="now">The current time.</param>
    public void RecordFailure(Guid listingId, DateTimeOffset now) => this._retryAfter[listingId] = now + this._delay;

    /// <summary>
    /// Notes that the listing was handled, so it is no longer remembered.
    /// </summary>
    /// <param name="listingId">The listing id.</param>
    public void RecordSuccess(Guid listingId) => this._retryAfter.TryRemove(listingId, out _);

    /// <summary>
    /// Gets the listings which should be skipped right now. Entries whose delay has passed are forgotten, so those listings
    /// are tried again.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>The ids of the listings to skip.</returns>
    public Guid[] GetSkippedIds(DateTimeOffset now)
    {
        var skipped = new List<Guid>();
        foreach (var entry in this._retryAfter)
        {
            if (entry.Value > now)
            {
                skipped.Add(entry.Key);
            }
            else
            {
                this._retryAfter.TryRemove(entry.Key, out _);
            }
        }

        return skipped.ToArray();
    }
}
