// <copyright file="AntiCheatState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// The process-wide state of the generic protective measures (all game servers share it).
/// </summary>
public static class AntiCheatState
{
    private static readonly Meter AntiCheatMeter = new("MUnique.OpenMU.AntiCheat");
    private static readonly byte[] Salt = RandomNumberGenerator.GetBytes(16);
    private static readonly object SweeperSync = new();
    private static SemaphoreSlim? passwordChecks;
    private static bool sweeperStarted;

    /// <summary>Gets the login throttle.</summary>
    public static LoginThrottle Login { get; } = new();

    /// <summary>Gets the connection tracker.</summary>
    public static ConnectionTracker Connections { get; } = new();

    /// <summary>Gets the compiled default configuration (everything in shadow mode).</summary>
    public static AntiCheatConfiguration DefaultConfiguration { get; } = new();

    /// <summary>Gets the counter of failed logins.</summary>
    public static Counter<int> LoginFailures { get; } = AntiCheatMeter.CreateCounter<int>("login_failures");

    /// <summary>Gets the counter of blocked (or, in shadow mode, would-be blocked) logins.</summary>
    public static Counter<int> LoginBlocked { get; } = AntiCheatMeter.CreateCounter<int>("login_blocked");

    /// <summary>Gets the counter of password check queue timeouts.</summary>
    public static Counter<int> PasswordCheckTimeouts { get; } = AntiCheatMeter.CreateCounter<int>("password_check_queue_timeouts");

    /// <summary>Gets the counter of refused (or would-be refused) connections.</summary>
    public static Counter<int> ConnectionsRefused { get; } = AntiCheatMeter.CreateCounter<int>("conn_refused");

    /// <summary>Gets the counter of closed (or would-be closed) connections.</summary>
    public static Counter<int> ConnectionsExpired { get; } = AntiCheatMeter.CreateCounter<int>("conn_expired");

    /// <summary>Gets the counter of observed client builds.</summary>
    public static Counter<int> BuildsObserved { get; } = AntiCheatMeter.CreateCounter<int>("build_observed");

    /// <summary>Gets the counter of observed client builds which are not in the list of allowed builds.</summary>
    public static Counter<int> BuildsUnknown { get; } = AntiCheatMeter.CreateCounter<int>("build_unknown");

    /// <summary>Gets the counter of received pings.</summary>
    public static Counter<int> PingsReceived { get; } = AntiCheatMeter.CreateCounter<int>("ping_received");

    /// <summary>
    /// Gets the active configuration of the given game context, or the compiled defaults.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The configuration.</returns>
    public static AntiCheatConfiguration GetConfiguration(IGameContext? gameContext)
    {
        return gameContext?.FeaturePlugIns.GetPlugIn<AntiCheatPlugIn>()?.Configuration ?? DefaultConfiguration;
    }

    /// <summary>
    /// Creates a short, salted hash of an account name for log lines (players sometimes type passwords into the name field).
    /// </summary>
    /// <param name="accountName">The account name.</param>
    /// <returns>A short hexadecimal hash.</returns>
    public static string HashForLog(string accountName)
    {
        var bytes = Encoding.UTF8.GetBytes(accountName.ToLowerInvariant());
        using var sha = SHA256.Create();
        sha.TransformBlock(Salt, 0, Salt.Length, null, 0);
        sha.TransformFinalBlock(bytes, 0, bytes.Length);
        return Convert.ToHexString(sha.Hash!, 0, 6).ToLowerInvariant();
    }

    /// <summary>
    /// Waits for a free slot to run a (CPU intensive) password check.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns><c>true</c> if a slot was acquired and must be released; otherwise <c>false</c>.</returns>
    public static async ValueTask<bool> TryEnterPasswordCheckAsync(AntiCheatConfiguration configuration)
    {
        var semaphore = LazyInitializer.EnsureInitialized(ref passwordChecks, () => new SemaphoreSlim(Math.Max(1, configuration.LoginMaxConcurrentPasswordChecks)));
        return await semaphore.WaitAsync(TimeSpan.FromMilliseconds(Math.Max(1, configuration.LoginPasswordCheckQueueTimeoutMs))).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases a slot acquired by <see cref="TryEnterPasswordCheckAsync"/>.
    /// </summary>
    public static void ExitPasswordCheck() => passwordChecks?.Release();

    /// <summary>
    /// Starts (once) the background loop which closes connections which did not log in or are silent, and cleans up old entries.
    /// </summary>
    /// <param name="configurationProvider">Gets the current configuration.</param>
    /// <param name="logger">The logger.</param>
    public static void EnsureSweeperStarted(Func<AntiCheatConfiguration> configurationProvider, ILogger logger)
    {
        lock (SweeperSync)
        {
            if (sweeperStarted)
            {
                return;
            }

            sweeperStarted = true;
        }

        _ = Task.Run(async () =>
        {
            var ticks = 0;
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                try
                {
                    await SweepOnceAsync(configurationProvider(), logger).ConfigureAwait(false);
                    if (++ticks % 12 == 0)
                    {
                        Login.Sweep(TimeSpan.FromHours(1));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[AC] sweeper failed.");
                }
            }
        });
    }

    /// <summary>
    /// Runs one sweep: closes (in enforce mode) or only logs (in shadow mode) expired connections.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The task.</returns>
    public static async ValueTask SweepOnceAsync(AntiCheatConfiguration configuration, ILogger logger)
    {
        foreach (var (connection, reason) in Connections.FindExpired(configuration))
        {
            var mode = reason == "prelogin" ? configuration.ConnectionMode : configuration.IdleMode;
            if (mode == AntiCheatMode.Off)
            {
                continue;
            }

            ConnectionsExpired.Add(1);
            logger.LogInformation("[AC] rule=conn.expired reason={Reason} mode={Mode} server={Server} ip={Ip} action={Action}", reason, mode, connection.ServerId, connection.Address, mode == AntiCheatMode.Enforce ? "close" : "allow");
            if (mode == AntiCheatMode.Enforce && connection.Close is { } close)
            {
                try
                {
                    await close().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "[AC] closing a connection failed.");
                }
            }
        }
    }
}
