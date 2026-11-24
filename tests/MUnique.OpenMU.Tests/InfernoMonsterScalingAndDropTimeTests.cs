// <copyright file="InfernoMonsterScalingAndDropTimeTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Verifies the 30 second item drop time and the monster scaling which is limited to server 1.
/// </summary>
[TestFixture]
public class InfernoMonsterScalingAndDropTimeTests
{
    /// <summary>
    /// An existing database receives the new settings exactly once, and later edits are kept.
    /// </summary>
    [Test]
    public async Task StartupInstallerUpdatesAnExistingDatabaseOnce()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        configuration.ItemDropDuration = TimeSpan.FromSeconds(60);
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.InfernoMonsterScalingAndDropTime)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await InfernoMonsterScalingAndDropTimeInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        Assert.That(configuration.ItemDropDuration, Is.EqualTo(TimeSpan.FromSeconds(30)));

        var plugInConfiguration = configuration.PlugInConfigurations.Single(c => c.TypeId == typeof(MonsterAttributeScaler).GUID);
        Assert.That(plugInConfiguration.IsActive, Is.True);
        var scaler = plugInConfiguration.GetConfiguration<MonsterAttributeScalerConfiguration>(null)!;
        Assert.That(scaler.OnlyServerId, Is.EqualTo((byte)0));
        Assert.That(scaler.AttackRatePercentage, Is.EqualTo(50f));
        Assert.That(scaler.HealthPercentage, Is.EqualTo(20f));
        Assert.That(scaler.DamagePercentage, Is.EqualTo(0f));
        Assert.That(scaler.DefensePercentage, Is.EqualTo(0f));
        Assert.That(scaler.DefenseRatePercentage, Is.EqualTo(0f));
        Assert.That(scaler.ScaleAllPercentage, Is.EqualTo(0f));

        configuration.ItemDropDuration = TimeSpan.FromSeconds(45);
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await InfernoMonsterScalingAndDropTimeInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
        Assert.That(configuration.ItemDropDuration, Is.EqualTo(TimeSpan.FromSeconds(45)));
    }

    /// <summary>
    /// The scaler applies to every server unless a server id is configured, and then only to that server.
    /// </summary>
    [Test]
    public void ScalerAppliesOnlyToTheConfiguredServer()
    {
        var configuration = new MonsterAttributeScalerConfiguration { OnlyServerId = 0 };
        Assert.That(configuration.OnlyServerId, Is.EqualTo((byte)0));
        Assert.That(new MonsterAttributeScalerConfiguration().OnlyServerId, Is.Null);
    }
}
