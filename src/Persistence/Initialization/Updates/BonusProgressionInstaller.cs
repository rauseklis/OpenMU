// <copyright file="BonusProgressionInstaller.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Installs bonus progression before configuration is loaded.
/// </summary>
public static class BonusProgressionInstaller
{
    /// <summary>
    /// Installs the reviewed opening pools and monster assignments once while preserving later configuration edits.
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
        if (updates.Any(update => update.Version == (int)UpdateVersion.BonusProgression && update.InstalledAt is not null))
        {
            return false;
        }

        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        await new ConfigureBonusProgressionUpdatePlugIn().ApplyUpdateAsync(context, configuration).ConfigureAwait(false);
        state.CurrentInstalledVersion = Math.Max(state.CurrentInstalledVersion, (int)UpdateVersion.BonusProgression);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }
}
