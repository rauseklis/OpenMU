// <copyright file="FinalSaveHealth.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Threading;

/// <summary>
/// Counts the players whose progress could not be saved when they left the game, even after retrying.
/// Their last changes are lost; the count makes that visible to the operator instead of only a log line.
/// </summary>
public static class FinalSaveHealth
{
    private static long failureCount;
    private static string lastFailedSubject = string.Empty;
    private static long lastFailureTicks;

    /// <summary>
    /// Gets the number of final saves which failed for good since the process started (or since <see cref="Reset"/>).
    /// </summary>
    public static long FailureCount => Interlocked.Read(ref failureCount);

    /// <summary>
    /// Gets the subject (the player) of the last failed final save.
    /// </summary>
    public static string LastFailedSubject => Volatile.Read(ref lastFailedSubject);

    /// <summary>
    /// Gets the time of the last failed final save, if any.
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
    /// Records a final save which failed for good.
    /// </summary>
    /// <param name="subject">The player.</param>
    public static void RecordFailure(string subject)
    {
        Interlocked.Increment(ref failureCount);
        Interlocked.Exchange(ref lastFailureTicks, DateTime.UtcNow.Ticks);
        Volatile.Write(ref lastFailedSubject, subject);
    }

    /// <summary>
    /// Resets the state. Meant for tests and for an operator who acknowledged a failure.
    /// </summary>
    public static void Reset()
    {
        Interlocked.Exchange(ref failureCount, 0);
        Interlocked.Exchange(ref lastFailureTicks, 0);
        Volatile.Write(ref lastFailedSubject, string.Empty);
    }
}
