// <copyright file="AncientTierBoxUpdateInstaller.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Applies only the approved Ancient reward update before game configuration is loaded at startup.
/// </summary>
public static class AncientTierBoxUpdateInstaller
{
    /// <summary>
    /// Installs the reward update once on an existing Season 6 database.
    /// </summary>
    /// <param name="provider">The persistence provider.</param>
    /// <returns>Whether the update was installed.</returns>
    public static async ValueTask<bool> ApplyIfNeededAsync(IPersistenceContextProvider provider)
    {
        using var context = provider.CreateNewContext();
        // Startup is still constructing the provider; live callbacks would resolve it recursively.
        using var notifications = context.SuspendChangeNotifications();
        var state = (await context.GetAsync<ConfigurationUpdateState>().ConfigureAwait(false)).FirstOrDefault();
        if (state?.InitializationKey != VersionSeasonSix.DataInitialization.Id)
        {
            return false;
        }

        var updates = await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false);
        if (updates.Any(update => update.Version == (int)UpdateVersion.AncientTierBoxes && update.InstalledAt is not null))
        {
            return false;
        }

        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        await new AddAncientTierBoxesUpdatePlugIn().ApplyUpdateAsync(context, configuration).ConfigureAwait(false);
        state.CurrentInstalledVersion = Math.Max(state.CurrentInstalledVersion, (int)UpdateVersion.AncientTierBoxes);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }
}
