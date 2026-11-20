// <copyright file="CombatEventHealth.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

using System.Threading;

/// <summary>
/// Tells whether the consumers of <see cref="CombatEvent"/>s work. A failing consumer never stops or changes
/// the game; it only makes the monitoring state visible here.
/// </summary>
public static class CombatEventHealth
{
    private static long failureCount;
    private static long lastFailureTicks;
    private static string lastFailure = string.Empty;

    /// <summary>
    /// Gets the number of consumer failures since the process started (or since <see cref="Reset"/>).
    /// </summary>
    public static long FailureCount => Interlocked.Read(ref failureCount);

    /// <summary>
    /// Gets a value indicating whether at least one consumer failure happened. Cases which depend on
    /// events from a time with failures are incomplete and must not be used for sanctions.
    /// </summary>
    public static bool IsDegraded => FailureCount > 0;

    /// <summary>
    /// Gets the time of the last failure, if any.
    /// </summary>
    public static DateTime? LastFailureUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref lastFailureTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Gets a short description of the last failure.
    /// </summary>
    public static string LastFailure => Volatile.Read(ref lastFailure);

    /// <summary>
    /// Records a failure of a consumer.
    /// </summary>
    /// <param name="exception">The exception.</param>
    public static void RecordFailure(Exception exception)
    {
        Interlocked.Increment(ref failureCount);
        Interlocked.Exchange(ref lastFailureTicks, DateTime.UtcNow.Ticks);
        Volatile.Write(ref lastFailure, exception.GetType().Name + ": " + exception.Message);
    }

    /// <summary>
    /// Resets the state. Meant for tests and for an operator who acknowledged a failure.
    /// </summary>
    public static void Reset()
    {
        Interlocked.Exchange(ref failureCount, 0);
        Interlocked.Exchange(ref lastFailureTicks, 0);
        Volatile.Write(ref lastFailure, string.Empty);
    }
}
