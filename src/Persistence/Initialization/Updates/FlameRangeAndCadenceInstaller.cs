// <copyright file="FlameRangeAndCadenceInstaller.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Installs the corrected Scroll of Flame range and hit cadence before loading game state.
/// </summary>
public static class FlameRangeAndCadenceInstaller
{
    /// <summary>
    /// Installs the correction once, preserving later administrator edits.
    /// </summary>
    /// <param name="provider">The persistence provider.</param>
    /// <returns>Whether the update was installed.</returns>
    public static async ValueTask<bool> ApplyIfNeededAsync(IPersistenceContextProvider provider)
    {
        using var context = provider.CreateNewContext();
        using var notifications = context.SuspendChangeNotifications();
        var state = (await context.GetAsync<ConfigurationUpdateState>().ConfigureAwait(false)).FirstOrDefault();
        if (state?.InitializationKey != VersionSeasonSix.DataInitialization.Id)
        {
            return false;
        }

        var updates = await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false);
        if (updates.Any(update => update.Version == (int)UpdateVersion.FlameRangeAndCadence && update.InstalledAt is not null))
        {
            return false;
        }

        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        await new FlameRangeAndCadenceUpdatePlugIn().ApplyUpdateAsync(context, configuration).ConfigureAwait(false);
        state.CurrentInstalledVersion = Math.Max(state.CurrentInstalledVersion, (int)UpdateVersion.FlameRangeAndCadence);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }
}
