// <copyright file="FailureWindows.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

using System.Collections.Concurrent;

/// <summary>
/// Counts events per key in sliding time windows and remembers lock expiries. The number of keys is bounded.
/// </summary>
public sealed class FailureWindows
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Func<DateTime> _clock;
    private readonly int _maxEntries;

    /// <summary>
    /// Initializes a new instance of the <see cref="FailureWindows"/> class.
    /// </summary>
    /// <param name="clock">The clock (UTC).</param>
    /// <param name="maxEntries">The maximum number of remembered keys.</param>
    public FailureWindows(Func<DateTime>? clock = null, int maxEntries = 100_000)
    {
        this._clock = clock ?? (() => DateTime.UtcNow);
        this._maxEntries = Math.Max(16, maxEntries);
    }

    /// <summary>Gets the number of remembered keys.</summary>
    public int Count => this._entries.Count;

    /// <summary>
    /// Records an event and returns how many events of this key happened within the window (including this one).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="window">The window.</param>
    /// <returns>The number of events in the window.</returns>
    public int Add(string key, TimeSpan window)
    {
        var now = this._clock();
        var entry = this._entries.GetOrAdd(key, _ => new Entry());
        int count;
        lock (entry)
        {
            Prune(entry, now, window);
            entry.Times.Enqueue(now);
            entry.LastTouch = now;
            count = entry.Times.Count;
        }

        if (this._entries.Count > this._maxEntries)
        {
            this.Evict(now);
        }

        return count;
    }

    /// <summary>
    /// Gets how many events of this key happened within the window.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="window">The window.</param>
    /// <returns>The number of events.</returns>
    public int CountInWindow(string key, TimeSpan window)
    {
        if (!this._entries.TryGetValue(key, out var entry))
        {
            return 0;
        }

        lock (entry)
        {
            Prune(entry, this._clock(), window);
            return entry.Times.Count;
        }
    }

    /// <summary>
    /// Locks the key for the given duration.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="duration">The duration.</param>
    public void Lock(string key, TimeSpan duration)
    {
        var now = this._clock();
        var entry = this._entries.GetOrAdd(key, _ => new Entry());
        lock (entry)
        {
            entry.LockedUntil = now + duration;
            entry.LastTouch = now;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the key is currently locked.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><c>true</c> if locked.</returns>
    public bool IsLocked(string key)
    {
        if (!this._entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        lock (entry)
        {
            return entry.LockedUntil > this._clock();
        }
    }

    /// <summary>
    /// Forgets the key.
    /// </summary>
    /// <param name="key">The key.</param>
    public void Remove(string key) => this._entries.TryRemove(key, out _);

    /// <summary>
    /// Removes keys which are not locked and were not touched for longer than the given age.
    /// </summary>
    /// <param name="maxAge">The maximum age.</param>
    public void Sweep(TimeSpan maxAge)
    {
        var now = this._clock();
        foreach (var pair in this._entries)
        {
            lock (pair.Value)
            {
                if (pair.Value.LockedUntil <= now && now - pair.Value.LastTouch > maxAge)
                {
                    this._entries.TryRemove(pair.Key, out _);
                }
            }
        }
    }

    private static void Prune(Entry entry, DateTime now, TimeSpan window)
    {
        while (entry.Times.Count > 0 && now - entry.Times.Peek() >= window)
        {
            entry.Times.Dequeue();
        }
    }

    private void Evict(DateTime now)
    {
        // Drop the least recently touched unlocked keys until the bound is met again.
        var excess = (this._entries.Count - this._maxEntries) + (this._maxEntries / 10);
        foreach (var pair in this._entries.OrderBy(p => p.Value.LastTouch).Take(excess))
        {
            if (pair.Value.LockedUntil <= now)
            {
                this._entries.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed class Entry
    {
        public Queue<DateTime> Times { get; } = new();

        public DateTime LockedUntil { get; set; } = DateTime.MinValue;

        public DateTime LastTouch { get; set; } = DateTime.MinValue;
    }
}
