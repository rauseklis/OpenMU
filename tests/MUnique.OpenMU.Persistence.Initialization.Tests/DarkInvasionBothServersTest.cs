// <copyright file="DarkInvasionBothServersTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests that the Dark Skeleton invasion runs on both game servers, with half-strength monsters on server 1.
/// </summary>
[TestFixture]
internal class DarkInvasionBothServersTest
{
    /// <summary>
    /// A new database gets the invasion on servers 0 and 1, with half strength on server 1.
    /// </summary>
    [Test]
    public async Task NewDatabaseRunsOnBothServersAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var configuration = await CreateConfigurationAsync(provider).ConfigureAwait(false);

        var invasion = GetPlugInConfiguration(configuration).GetConfiguration<DarkSkeletonInvasionConfiguration>(DarkInvasionBothServersUpdatePlugIn.ServerLikeReferenceHandler.Instance)!;
        Assert.That(invasion.GameServerIds, Is.EqualTo(new byte[] { 0, 1 }));
        Assert.That(invasion.WeakerGameServerIds, Is.EqualTo(new byte[] { 1 }));
        Assert.That(invasion.WeakerStrengthPercentage, Is.EqualTo(50f));
    }

    /// <summary>
    /// The update adds server 1 to a configuration which runs only on server 0 (like the live server),
    /// sets the half strength there and keeps the other settings, also when applied twice.
    /// </summary>
    /// <param name="withReferenceMetadata">Whether the stored configuration has reference metadata, as written by the server.</param>
    [TestCase(true)]
    [TestCase(false)]
    public async Task UpdateAddsServerOneAndKeepsOtherSettingsAsync(bool withReferenceMetadata)
    {
        var provider = new InMemoryPersistenceContextProvider();
        var configuration = await CreateConfigurationAsync(provider).ConfigureAwait(false);
        var plugInConfiguration = GetPlugInConfiguration(configuration);

        var live = DarkSkeletonInvasionConfiguration.Default;
        live.GameServerIds = [0];
        live.WeakerGameServerIds = [];
        live.WeakerStrengthPercentage = 100;
        live.Timetable = [new TimeOnly(21, 30)];
        plugInConfiguration.SetConfiguration(live, withReferenceMetadata ? DarkInvasionBothServersUpdatePlugIn.ServerLikeReferenceHandler.Instance : null);

        var update = new DarkInvasionBothServersUpdatePlugIn();
        await update.ApplyUpdateAsync(provider.CreateNewContext(), configuration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(provider.CreateNewContext(), configuration).ConfigureAwait(false);

        var invasion = plugInConfiguration.GetConfiguration<DarkSkeletonInvasionConfiguration>(DarkInvasionBothServersUpdatePlugIn.ServerLikeReferenceHandler.Instance)!;
        Assert.That(invasion.GameServerIds, Is.EqualTo(new byte[] { 0, 1 }));
        Assert.That(invasion.WeakerGameServerIds, Is.EqualTo(new byte[] { 1 }));
        Assert.That(invasion.WeakerStrengthPercentage, Is.EqualTo(50f));
        Assert.That(invasion.Timetable, Is.EqualTo(new[] { new TimeOnly(21, 30) }), "Other settings are kept.");
        Assert.That(update.IsMandatory, Is.True);
    }

    private static PlugInConfiguration GetPlugInConfiguration(GameConfiguration configuration)
        => configuration.PlugInConfigurations.Single(c => c.TypeId == typeof(DarkSkeletonInvasionPlugIn).GUID);

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider provider)
    {
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
