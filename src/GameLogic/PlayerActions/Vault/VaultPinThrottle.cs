// <copyright file="VaultPinThrottle.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Vault;

using System.Collections.Concurrent;

/// <summary>
/// Limits the tries to guess the PIN of a vault. The tries are counted per account, not per connection, so
/// that logging in again does not start a new set of tries. After too many wrong tries, the account can not try
/// for a while, even with the correct PIN.
/// </summary>
public sealed class VaultPinThrottle
{
    private const int MaximumRememberedAccounts = 100_000;

    private readonly ConcurrentDictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<DateTime> _clock;
    private readonly int _maximumFailures;
    private readonly TimeSpan _lockDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="VaultPinThrottle"/> class.
    /// </summary>
    /// <param name="clock">The clock (UTC); the system clock when <see langword="null"/>.</param>
    /// <param name="maximumFailures">The wrong tries which are allowed in a row.</param>
    /// <param name="lockDuration">How long no try is possible after the wrong tries.</param>
    public VaultPinThrottle(Func<DateTime>? clock = null, int maximumFailures = 5, TimeSpan? lockDuration = null)
    {
        this._clock = clock ?? (() => DateTime.UtcNow);
        this._maximumFailures = maximumFailures;
        this._lockDuration = lockDuration ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Gets the throttle which is used by default (shared by all game servers of the process).
    /// </summary>
    public static VaultPinThrottle Shared { get; } = new();

    /// <summary>
    /// Determines whether the account may try a PIN now.
    /// </summary>
    /// <param name="account">The login name of the account.</param>
    /// <returns><see langword="true"/> if a try is allowed.</returns>
    public bool IsAllowed(string account)
    {
        return !this._states.TryGetValue(account, out var state) || state.LockedUntil <= this._clock();
    }

    /// <summary>
    /// Records the result of a try.
    /// </summary>
    /// <param name="account">The login name of the account.</param>
    /// <param name="success">Whether the PIN was correct.</param>
    public void Record(string account, bool success)
    {
        if (success)
        {
            this._states.TryRemove(account, out _);
            return;
        }

        if (this._states.Count >= MaximumRememberedAccounts && !this._states.ContainsKey(account))
        {
            return;
        }

        this._states.AddOrUpdate(
            account,
            _ => new State(1, this._maximumFailures <= 1 ? this._clock() + this._lockDuration : DateTime.MinValue),
            (_, old) =>
            {
                var failures = old.LockedUntil > DateTime.MinValue && old.LockedUntil <= this._clock() ? 1 : old.Failures + 1;
                return failures >= this._maximumFailures
                    ? new State(0, this._clock() + this._lockDuration)
                    : new State(failures, DateTime.MinValue);
            });
    }

    private sealed record State(int Failures, DateTime LockedUntil);
}
