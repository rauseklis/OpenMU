// <copyright file="InfernoGoldenMonstersTests.cs" company="MUnique">
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
/// Verifies the update which boosts the Golden monsters of server 1.
/// </summary>
[TestFixture]
public class InfernoGoldenMonstersTests
{
    /// <summary>
    /// The update sets only the boosted values, keeps the others and runs once.
    /// </summary>
    [Test]
    public async Task UpdateSetsOnlyTheBoostedValuesOnce()
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
                AttackRatePercentage = 150f,
                DefenseRatePercentage = 0f,
                DefensePercentage = 0f,
                HealthPercentage = 20f,
                OnlyServerId = 0,
            },
            null);
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.InfernoGoldenMonsters)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await InfernoGoldenMonstersInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);

        var scaler = plugInConfiguration.GetConfiguration<MonsterAttributeScalerConfiguration>(null)!;
        Assert.That(scaler.BoostedMonsterNumbers, Is.EqualTo("43,53,54,78,79,80,81,82,83"));
        Assert.That(scaler.BoostedDamagePercentage, Is.EqualTo(100f));
        Assert.That(scaler.BoostedAttackRatePercentage, Is.EqualTo(900f));
        Assert.That(scaler.BoostedDefenseRatePercentage, Is.EqualTo(900f));
        Assert.That(scaler.BoostedDefensePercentage, Is.EqualTo(900f));
        Assert.That(scaler.BoostedHealthPercentage, Is.EqualTo(900f));
        Assert.That(scaler.AttackRatePercentage, Is.EqualTo(150f));
        Assert.That(scaler.HealthPercentage, Is.EqualTo(20f));
        Assert.That(scaler.OnlyServerId, Is.EqualTo((byte)0));

        Assert.That(await InfernoGoldenMonstersInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
    }
}
