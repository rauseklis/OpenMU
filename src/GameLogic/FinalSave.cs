// <copyright file="FinalSave.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using Microsoft.Extensions.Logging;

/// <summary>
/// Runs the save which happens when a player leaves the game. A failed save (an exception, or a
/// <see langword="false"/> result) is retried a few times, because a short database problem should not cost
/// the player their progress; if it keeps failing, it is counted in <see cref="FinalSaveHealth"/>.
/// </summary>
internal static class FinalSave
{
    private static readonly TimeSpan[] DefaultDelays =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
    ];

    /// <summary>
    /// Runs the save until it succeeds or all retries are used up.
    /// </summary>
    /// <param name="save">The save operation; returns <see langword="true"/> on success.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="subject">A description of the player, for the log and the health state.</param>
    /// <param name="delays">The waiting times between the attempts; the default when <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the progress was saved.</returns>
    public static async ValueTask<bool> RunAsync(Func<ValueTask<bool>> save, ILogger logger, string subject, IReadOnlyList<TimeSpan>? delays = null)
    {
        delays ??= DefaultDelays;
        for (var attempt = 0; attempt <= delays.Count; attempt++)
        {
            try
            {
                if (await save().ConfigureAwait(false))
                {
                    return true;
                }

                logger.LogWarning("Saving the progress of {Player} when leaving the game did not succeed (attempt {Attempt}).", subject, attempt + 1);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Saving the progress of {Player} when leaving the game failed (attempt {Attempt}).", subject, attempt + 1);
            }

            if (attempt < delays.Count)
            {
                await Task.Delay(delays[attempt]).ConfigureAwait(false);
            }
        }

        FinalSaveHealth.RecordFailure(subject);
        logger.LogCritical("The progress of {Player} could not be saved when leaving the game, even after {Attempts} attempts. The changes since its last successful save are lost.", subject, delays.Count + 1);
        return false;
    }
}
