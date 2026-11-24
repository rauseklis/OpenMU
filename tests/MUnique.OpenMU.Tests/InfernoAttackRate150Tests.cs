// <copyright file="InfernoAttackRate150Tests.cs" company="MUnique">
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
/// Verifies the update which raises the attack rate increase of server 1 to 150%.
/// </summary>
[TestFixture]
public class InfernoAttackRate150Tests
{
    /// <summary>
    /// The update changes only the attack rate, keeps the other values and runs once.
    /// </summary>
    [Test]
    public async Task UpdateRaisesOnlyTheAttackRateOnce()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var plugInConfiguration = configuration.PlugInConfigurations.Single(c => c.TypeId == typeof(MonsterAttributeScaler).GUID);
        plugInConfiguration.SetConfiguration(
            new MonsterAttributeScalerConfiguration
            {
                DamagePercentage = 0f,
                AttackRatePercentage = 50f,
                DefenseRatePercentage = 0f,
                DefensePercentage = 0f,
                HealthPercentage = 20f,
                OnlyServerId = 0,
            },
            null);
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.InfernoAttackRate150)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await InfernoAttackRate150Installer.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);

        var scaler = plugInConfiguration.GetConfiguration<MonsterAttributeScalerConfiguration>(null)!;
        Assert.That(plugInConfiguration.IsActive, Is.True);
        Assert.That(scaler.AttackRatePercentage, Is.EqualTo(150f));
        Assert.That(scaler.HealthPercentage, Is.EqualTo(20f));
        Assert.That(scaler.DamagePercentage, Is.EqualTo(0f));
        Assert.That(scaler.DefensePercentage, Is.EqualTo(0f));
        Assert.That(scaler.DefenseRatePercentage, Is.EqualTo(0f));
        Assert.That(scaler.OnlyServerId, Is.EqualTo((byte)0));

        Assert.That(await InfernoAttackRate150Installer.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
    }
}
