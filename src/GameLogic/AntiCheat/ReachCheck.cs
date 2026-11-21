// <copyright file="ReachCheck.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Checks that the player is close enough to the target of a melee hit. In shadow mode it only counts and logs.
/// </summary>
public static class ReachCheck
{
    private const int MaximumRememberedKeys = 10_000;

    private static readonly ConcurrentDictionary<string, DateTime> LastLogged = new(StringComparer.Ordinal);

    /// <summary>
    /// Determines whether the distance is beyond the allowed reach of the configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="distance">The distance between the player and the target, in tiles.</param>
    /// <returns><see langword="true"/> if the distance is too large.</returns>
    public static bool IsBeyondReach(AntiCheatConfiguration configuration, double distance)
        => configuration.HitReachMode != AntiCheatMode.Off && distance > configuration.HitReachMaximumDistance;

    /// <summary>
    /// Checks a melee hit. Counts (and, at most every ten seconds per account, logs) a hit beyond the reach.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="accountName">The login name of the account, which is only logged as a short hash.</param>
    /// <param name="distance">The distance between the player and the target, in tiles.</param>
    /// <returns><see langword="true"/> if the hit must be refused (only in enforce mode).</returns>
    public static bool ShouldRefuse(AntiCheatConfiguration configuration, ILogger logger, string accountName, double distance)
        => Evaluate("hit.reach", configuration.HitReachMode, configuration.HitReachMaximumDistance, AntiCheatState.HitsBeyondReach, logger, accountName, distance);

    /// <summary>
    /// Checks the drop of an item on the ground. Counts (and, at most every ten seconds per account, logs) a drop beyond the reach.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="accountName">The login name of the account, which is only logged as a short hash.</param>
    /// <param name="distance">The distance between the player and the target cell, in tiles.</param>
    /// <returns><see langword="true"/> if the drop must be refused (only in enforce mode).</returns>
    public static bool ShouldRefuseDrop(AntiCheatConfiguration configuration, ILogger logger, string accountName, double distance)
        => Evaluate("drop.reach", configuration.DropReachMode, configuration.DropReachMaximumDistance, AntiCheatState.DropsBeyondReach, logger, accountName, distance);

    private static bool Evaluate(string rule, AntiCheatMode mode, int maximumDistance, System.Diagnostics.Metrics.Counter<int> counter, ILogger logger, string accountName, double distance)
    {
        if (mode == AntiCheatMode.Off || distance <= maximumDistance)
        {
            return false;
        }

        counter.Add(1);
        var now = DateTime.UtcNow;
        var key = rule + "|" + AntiCheatState.HashForLog(accountName);
        if (LastLogged.Count < MaximumRememberedKeys && (!LastLogged.TryGetValue(key, out var last) || now - last > TimeSpan.FromSeconds(10)))
        {
            LastLogged[key] = now;
            logger.LogInformation(
                "[AC] rule={Rule} mode={Mode} account={Account} distance={Distance:F1} max={Max} action={Action}",
                rule,
                mode,
                AntiCheatState.HashForLog(accountName),
                distance,
                maximumDistance,
                mode == AntiCheatMode.Enforce ? "refuse" : "allow");
        }

        return mode == AntiCheatMode.Enforce;
    }
}
