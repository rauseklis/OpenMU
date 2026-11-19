// <copyright file="AntiCheatTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// Tests for the generic login and connection protection.
/// </summary>
[TestFixture]
public class AntiCheatTests
{
    private DateTime _now;

    private DateTime Clock() => this._now;

    /// <summary>
    /// Sets up the test clock.
    /// </summary>
    [SetUp]
    public void SetUp() => this._now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Events outside of the window are not counted.
    /// </summary>
    [Test]
    public void WindowForgetsOldEvents()
    {
        var windows = new FailureWindows(this.Clock);
        var window = TimeSpan.FromSeconds(10);
        Assert.That(windows.Add("a", window), Is.EqualTo(1));
        Assert.That(windows.Add("a", window), Is.EqualTo(2));
        this._now = this._now.AddSeconds(10);
        Assert.That(windows.Add("a", window), Is.EqualTo(1));
        Assert.That(windows.CountInWindow("b", window), Is.EqualTo(0));
    }

    /// <summary>
    /// The number of remembered keys stays bounded; locked keys are kept.
    /// </summary>
    [Test]
    public void WindowsAreBounded()
    {
        var windows = new FailureWindows(this.Clock, 100);
        windows.Lock("locked", TimeSpan.FromHours(1));
        for (var i = 0; i < 5000; i++)
        {
            this._now = this._now.AddMilliseconds(1);
            windows.Add("k" + i, TimeSpan.FromMinutes(1));
        }

        Assert.That(windows.Count, Is.LessThan(300));
        Assert.That(windows.IsLocked("locked"), Is.True);
    }

    /// <summary>
    /// Reaching the pair limit locks that pair only, until the lock expires.
    /// </summary>
    [Test]
    public void PairIsLockedAfterLimitAndUnlocksLater()
    {
        var throttle = new LoginThrottle(this.Clock);
        var config = new AntiCheatConfiguration { LoginPairMaxFailures = 3, LoginPairLockSeconds = 60 };
        for (var i = 0; i < 3; i++)
        {
            Assert.That(throttle.Check("victim", "1.1.1.1"), Is.EqualTo(LoginThrottleDecision.Allow));
            throttle.RecordFailure("victim", "1.1.1.1", config);
        }

        Assert.That(throttle.Check("victim", "1.1.1.1"), Is.EqualTo(LoginThrottleDecision.BlockedPair));
        Assert.That(throttle.Check("victim", "2.2.2.2"), Is.EqualTo(LoginThrottleDecision.Allow), "other addresses must not be locked out");
        Assert.That(throttle.Check("other", "1.1.1.1"), Is.EqualTo(LoginThrottleDecision.Allow));

        this._now = this._now.AddSeconds(61);
        Assert.That(throttle.Check("victim", "1.1.1.1"), Is.EqualTo(LoginThrottleDecision.Allow));
    }

    /// <summary>
    /// Many accounts from one address lock the address.
    /// </summary>
    [Test]
    public void AddressIsLockedWhenTryingManyAccounts()
    {
        var throttle = new LoginThrottle(this.Clock);
        var config = new AntiCheatConfiguration { LoginIpMaxFailures = 5, LoginPairMaxFailures = 100 };
        for (var i = 0; i < 5; i++)
        {
            throttle.RecordFailure("acct" + i, "9.9.9.9", config);
        }

        Assert.That(throttle.Check("anything", "9.9.9.9"), Is.EqualTo(LoginThrottleDecision.BlockedIp));
        Assert.That(throttle.Check("anything", "8.8.8.8"), Is.EqualTo(LoginThrottleDecision.Allow));
    }

    /// <summary>
    /// An account attacked from many addresses is slowed down but never locked.
    /// </summary>
    [Test]
    public void AccountAttackedFromManyAddressesIsOnlyDelayed()
    {
        var throttle = new LoginThrottle(this.Clock);
        var config = new AntiCheatConfiguration { LoginAccountTarpitFailures = 5, LoginTarpitDelayMs = 123, LoginPairMaxFailures = 100, LoginIpMaxFailures = 100 };
        var delay = 0;
        for (var i = 0; i < 6; i++)
        {
            delay = throttle.RecordFailure("victim", "10.0.0." + i, config);
        }

        Assert.That(delay, Is.EqualTo(123));
        Assert.That(throttle.Check("victim", "10.0.1.1"), Is.EqualTo(LoginThrottleDecision.Allow));
        Assert.That(throttle.Check("victim", "10.0.0.1"), Is.EqualTo(LoginThrottleDecision.Allow));
    }

    /// <summary>
    /// A success clears the pair counter.
    /// </summary>
    [Test]
    public void SuccessClearsPairFailures()
    {
        var throttle = new LoginThrottle(this.Clock);
        var config = new AntiCheatConfiguration { LoginPairMaxFailures = 3, LoginIpMaxFailures = 100 };
        throttle.RecordFailure("a", "1.1.1.1", config);
        throttle.RecordFailure("a", "1.1.1.1", config);
        throttle.RecordSuccess("a", "1.1.1.1");
        throttle.RecordFailure("a", "1.1.1.1", config);
        throttle.RecordFailure("a", "1.1.1.1", config);
        Assert.That(throttle.Check("a", "1.1.1.1"), Is.EqualTo(LoginThrottleDecision.Allow));
    }

    /// <summary>
    /// Trusted addresses get higher limits.
    /// </summary>
    [Test]
    public void TrustedAddressesGetHigherLimits()
    {
        var throttle = new LoginThrottle(this.Clock);
        var config = new AntiCheatConfiguration { LoginPairMaxFailures = 3, LoginTrustedMultiplier = 5, LoginTrustedIps = { "5.5.5.5" }, LoginIpMaxFailures = 1000 };
        for (var i = 0; i < 6; i++)
        {
            throttle.RecordFailure("a", "5.5.5.5", config);
        }

        Assert.That(throttle.Check("a", "5.5.5.5"), Is.EqualTo(LoginThrottleDecision.Allow));
        for (var i = 0; i < 9; i++)
        {
            throttle.RecordFailure("a", "5.5.5.5", config);
        }

        Assert.That(throttle.Check("a", "5.5.5.5"), Is.EqualTo(LoginThrottleDecision.BlockedPair));
    }

    /// <summary>
    /// The per-address limit counts open connections and returns to zero when they end.
    /// </summary>
    [Test]
    public void ConnectionsPerAddressAreLimitedAndReleased()
    {
        var tracker = new ConnectionTracker(this.Clock);
        var config = new AntiCheatConfiguration { ConnectionMaxConcurrentPerIp = 2, ConnectionMaxNewPerIpPerMinute = 1000 };
        var list = new List<TrackedConnection>();
        Assert.That(tracker.TryAccept("1.1.1.1", 1, config, out var c1), Is.EqualTo(ConnectionDecision.Allow));
        list.Add(c1);
        Assert.That(tracker.TryAccept("1.1.1.1", 2, config, out var c2), Is.EqualTo(ConnectionDecision.Allow));
        list.Add(c2);
        Assert.That(tracker.TryAccept("1.1.1.1", 3, config, out var c3), Is.EqualTo(ConnectionDecision.TooManyForAddress));
        tracker.Release(c3);
        Assert.That(tracker.TryAccept("2.2.2.2", 1, config, out var c4), Is.EqualTo(ConnectionDecision.Allow));
        list.Add(c4);

        tracker.Release(c1);
        Assert.That(tracker.TryAccept("1.1.1.1", 1, config, out var c5), Is.EqualTo(ConnectionDecision.Allow));
        list.Add(c5);

        foreach (var connection in list)
        {
            tracker.Release(connection);
            tracker.Release(connection); // releasing twice must be harmless
        }

        Assert.That(tracker.Count, Is.EqualTo(0));
    }

    /// <summary>
    /// Too many new connections within a minute are noticed.
    /// </summary>
    [Test]
    public void NewConnectionRateIsLimited()
    {
        var tracker = new ConnectionTracker(this.Clock);
        var config = new AntiCheatConfiguration { ConnectionMaxConcurrentPerIp = 1000, ConnectionMaxNewPerIpPerMinute = 3, ConnectionMaxPreLoginPerServer = 1000 };
        var decision = ConnectionDecision.Allow;
        for (var i = 0; i < 4; i++)
        {
            decision = tracker.TryAccept("3.3.3.3", 1, config, out var connection);
            tracker.Release(connection);
        }

        Assert.That(decision, Is.EqualTo(ConnectionDecision.TooManyRecentForAddress));
        this._now = this._now.AddMinutes(2);
        Assert.That(tracker.TryAccept("3.3.3.3", 1, config, out var later), Is.EqualTo(ConnectionDecision.Allow));
        tracker.Release(later);
    }

    /// <summary>
    /// Connections which did not log in count against the per-server cap until they log in.
    /// </summary>
    [Test]
    public void PreLoginCapFreesUpOnLogin()
    {
        var tracker = new ConnectionTracker(this.Clock);
        var config = new AntiCheatConfiguration { ConnectionMaxPreLoginPerServer = 2, ConnectionMaxConcurrentPerIp = 1000, ConnectionMaxNewPerIpPerMinute = 1000 };
        tracker.TryAccept("1.0.0.1", 1, config, out var a);
        tracker.TryAccept("1.0.0.2", 1, config, out _);
        Assert.That(tracker.TryAccept("1.0.0.3", 1, config, out var c), Is.EqualTo(ConnectionDecision.TooManyPreLogin));
        tracker.Release(c);
        Assert.That(tracker.TryAccept("1.0.0.3", 2, config, out var otherServer), Is.EqualTo(ConnectionDecision.Allow), "caps are per server");
        tracker.Release(otherServer);
        tracker.MarkLoggedIn(a);
        Assert.That(tracker.TryAccept("1.0.0.4", 1, config, out _), Is.EqualTo(ConnectionDecision.Allow));
    }

    /// <summary>
    /// Only connections which did not log in in time, or are silent, are reported.
    /// </summary>
    [Test]
    public void ExpiredConnectionsAreFoundByReason()
    {
        var tracker = new ConnectionTracker(this.Clock);
        var config = new AntiCheatConfiguration { ConnectionPreLoginTimeoutSeconds = 30, IdleNoPacketTimeoutSeconds = 100 };
        tracker.TryAccept("1.0.0.1", 1, config, out var silentPreLogin);
        tracker.TryAccept("1.0.0.2", 1, config, out var loggedIn);
        tracker.MarkLoggedIn(loggedIn);
        tracker.TryAccept("1.0.0.3", 1, config, out var talkative);
        tracker.MarkLoggedIn(talkative);

        this._now = this._now.AddSeconds(40);
        talkative.Touch(this._now);
        var expired = tracker.FindExpired(config);
        Assert.That(expired.Select(e => (e.Connection, e.Reason)), Is.EquivalentTo(new[] { (silentPreLogin, "prelogin") }));

        this._now = this._now.AddSeconds(101);
        expired = tracker.FindExpired(config);
        Assert.That(expired.Any(e => e.Connection == loggedIn && e.Reason == "idle"), Is.True);
        Assert.That(expired.Any(e => e.Connection == talkative && e.Reason == "idle"), Is.True);
    }

    /// <summary>
    /// Shadow mode never closes connections, enforce mode does, off does nothing.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SweepClosesOnlyInEnforceMode()
    {
        var closed = 0;
        var config = new AntiCheatConfiguration { ConnectionPreLoginTimeoutSeconds = 1, ConnectionMode = AntiCheatMode.Shadow };
        AntiCheatState.Connections.TryAccept("7.7.7.7", 99, config, out var connection);
        connection.Close = () =>
        {
            closed++;
            return ValueTask.CompletedTask;
        };

        try
        {
            // The shared tracker uses the real clock: wait until the connection is old enough.
            await Task.Delay(1200);
            await AntiCheatState.SweepOnceAsync(config, NullLogger.Instance);
            Assert.That(closed, Is.EqualTo(0), "shadow mode must not close");

            config.ConnectionMode = AntiCheatMode.Off;
            await AntiCheatState.SweepOnceAsync(config, NullLogger.Instance);
            Assert.That(closed, Is.EqualTo(0), "off must not close");

            config.ConnectionMode = AntiCheatMode.Enforce;
            await AntiCheatState.SweepOnceAsync(config, NullLogger.Instance);
            Assert.That(closed, Is.EqualTo(1));
        }
        finally
        {
            AntiCheatState.Connections.Release(connection);
        }
    }

    /// <summary>
    /// IPv4-mapped IPv6 addresses are treated as their IPv4 address.
    /// </summary>
    [Test]
    public void MappedAddressesAreNormalized()
    {
        var mapped = IPAddress.Parse("::ffff:1.2.3.4");
        Assert.That(ConnectionTracker.Normalize(mapped), Is.EqualTo("1.2.3.4"));
    }

    /// <summary>
    /// The log hash neither contains the name nor differs for different case.
    /// </summary>
    [Test]
    public void LogHashHidesTheName()
    {
        var hash = AntiCheatState.HashForLog("MyPassword123");
        Assert.That(hash, Does.Not.Contain("MyPassword"));
        Assert.That(hash, Has.Length.EqualTo(12));
        Assert.That(AntiCheatState.HashForLog("mypassword123"), Is.EqualTo(hash));
    }

    /// <summary>
    /// The compiled defaults start everything in shadow mode.
    /// </summary>
    [Test]
    public void DefaultsAreShadow()
    {
        var config = new AntiCheatConfiguration();
        Assert.That(config.LoginMode, Is.EqualTo(AntiCheatMode.Shadow));
        Assert.That(config.ConnectionMode, Is.EqualTo(AntiCheatMode.Shadow));
        Assert.That(config.IdleMode, Is.EqualTo(AntiCheatMode.Shadow));
    }
}
