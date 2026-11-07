// <copyright file="ReplayCache.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Security;

using System.Collections.Concurrent;

/// <summary>
/// Remembers the signatures of accepted requests for a little longer than the timestamp window,
/// so a captured request cannot be sent again.
/// </summary>
public sealed class ReplayCache
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly TimeSpan _lifetime = TimeSpan.FromSeconds((RequestSigner.AllowedClockSkewSeconds * 2) + 5);
    private DateTimeOffset _nextCleanup = DateTimeOffset.MinValue;

    /// <summary>
    /// Registers the signature.
    /// </summary>
    /// <param name="signature">The signature.</param>
    /// <param name="now">The current time.</param>
    /// <returns><c>true</c> if it is new; <c>false</c> if the same signature was already accepted within the window.</returns>
    public bool TryRegister(string signature, DateTimeOffset now)
    {
        this.Cleanup(now);
        var key = signature.ToLowerInvariant();
        var added = true;
        this._seen.AddOrUpdate(
            key,
            _ => now + this._lifetime,
            (_, expires) =>
            {
                if (expires > now)
                {
                    added = false;
                    return expires;
                }

                return now + this._lifetime;
            });
        return added;
    }

    private void Cleanup(DateTimeOffset now)
    {
        if (now < this._nextCleanup)
        {
            return;
        }

        this._nextCleanup = now.AddSeconds(30);
        foreach (var pair in this._seen)
        {
            if (pair.Value <= now)
            {
                this._seen.TryRemove(pair);
            }
        }
    }
}
