// <copyright file="LoginThrottle.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// Limits failed login attempts. Locks are per (account, address) pair and per address; an account which is
/// attacked from many addresses is only slowed down (never locked), so that nobody can lock out somebody else.
/// </summary>
public sealed class LoginThrottle
{
    private readonly FailureWindows _pairs;
    private readonly FailureWindows _addresses;
    private readonly FailureWindows _accounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginThrottle"/> class.
    /// </summary>
    /// <param name="clock">The clock (UTC).</param>
    /// <param name="maxEntries">The bound of remembered keys per category.</param>
    public LoginThrottle(Func<DateTime>? clock = null, int maxEntries = 100_000)
    {
        this._pairs = new FailureWindows(clock, maxEntries);
        this._addresses = new FailureWindows(clock, maxEntries);
        this._accounts = new FailureWindows(clock, maxEntries);
    }

    /// <summary>
    /// Checks whether a login attempt may proceed. Does not count anything.
    /// </summary>
    /// <param name="account">The account name.</param>
    /// <param name="address">The remote address.</param>
    /// <returns>The decision (ignoring the mode, which is applied by the caller).</returns>
    public LoginThrottleDecision Check(string account, string address)
    {
        if (this._addresses.IsLocked(NormalizeAddress(address)))
        {
            return LoginThrottleDecision.BlockedIp;
        }

        if (this._pairs.IsLocked(PairKey(account, address)))
        {
            return LoginThrottleDecision.BlockedPair;
        }

        return LoginThrottleDecision.Allow;
    }

    /// <summary>
    /// Records a failed attempt and sets locks when limits are reached.
    /// </summary>
    /// <param name="account">The account name.</param>
    /// <param name="address">The remote address.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The delay in milliseconds the answer should be held back (tarpit), or 0.</returns>
    public int RecordFailure(string account, string address, AntiCheatConfiguration configuration)
    {
        var factor = this.TrustFactor(address, configuration);
        var normalizedAddress = NormalizeAddress(address);

        var pairCount = this._pairs.Add(PairKey(account, address), TimeSpan.FromSeconds(configuration.LoginPairWindowSeconds));
        if (pairCount >= configuration.LoginPairMaxFailures * factor)
        {
            this._pairs.Lock(PairKey(account, address), TimeSpan.FromSeconds(configuration.LoginPairLockSeconds));
        }

        var addressCount = this._addresses.Add(normalizedAddress, TimeSpan.FromSeconds(configuration.LoginIpWindowSeconds));
        if (addressCount >= configuration.LoginIpMaxFailures * factor)
        {
            this._addresses.Lock(normalizedAddress, TimeSpan.FromSeconds(configuration.LoginIpLockSeconds));
        }

        var accountCount = this._accounts.Add(account.ToLowerInvariant(), TimeSpan.FromSeconds(configuration.LoginPairWindowSeconds));
        return accountCount >= configuration.LoginAccountTarpitFailures ? configuration.LoginTarpitDelayMs : 0;
    }

    /// <summary>
    /// Records a successful login. Clears the failures of the account/address pair only.
    /// </summary>
    /// <param name="account">The account name.</param>
    /// <param name="address">The remote address.</param>
    public void RecordSuccess(string account, string address)
    {
        this._pairs.Remove(PairKey(account, address));
    }

    /// <summary>
    /// Removes entries which are old and not locked.
    /// </summary>
    /// <param name="maxAge">The maximum age.</param>
    public void Sweep(TimeSpan maxAge)
    {
        this._pairs.Sweep(maxAge);
        this._addresses.Sweep(maxAge);
        this._accounts.Sweep(maxAge);
    }

    private static string NormalizeAddress(string address) => address.Trim().ToLowerInvariant();

    private static string PairKey(string account, string address) => account.ToLowerInvariant() + "|" + NormalizeAddress(address);

    private int TrustFactor(string address, AntiCheatConfiguration configuration)
    {
        var normalized = NormalizeAddress(address);
        return configuration.LoginTrustedIps.Any(ip => string.Equals(ip.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
            ? Math.Max(1, configuration.LoginTrustedMultiplier)
            : 1;
    }
}
