// <copyright file="HealthLossPerHitTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using HealthLossPerHit = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.HealthLossPerHit;

/// <summary>
/// Tests the health loss per hit of the Imp, the Demon and the wings.
/// </summary>
[TestFixture]
internal class HealthLossPerHitTest
{
    /// <summary>
    /// Tests the agreed values, so that a change of the table is noticed.
    /// </summary>
    [Test]
    public void TableHasTheAgreedValues()
    {
        var table = HealthLossPerHit.Items.ToDictionary(i => (i.Group, i.Number), i => i.Health);

        Assert.That(table, Has.Count.EqualTo(16));
        Assert.That(table[(13, 1)], Is.EqualTo(3f), "Imp (Satan)");
        Assert.That(table[(13, 64)], Is.EqualTo(4f), "Demon");
        foreach (var number in new short[] { 0, 1, 2, 41 })
        {
            Assert.That(table[(12, number)], Is.EqualTo(1f), $"1st class wing {number}");
        }

        foreach (var number in new short[] { 3, 4, 5, 6, 42 })
        {
            Assert.That(table[(12, number)], Is.EqualTo(3f), $"2nd class wing {number}");
        }

        foreach (var number in new short[] { 36, 37, 38, 39, 43 })
        {
            Assert.That(table[(12, number)], Is.EqualTo(5f), $"3rd class wing {number}");
        }
    }

    /// <summary>
    /// Tests that a new season 6 database has the health loss on exactly the listed items.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheHealthLossAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        AssertOnlyListedItemsLoseHealth(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update fixes an existing database, changes nothing else and can run twice without doubling the loss.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateSetsTheHealthLossOnlyOnTheListedItemsAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        RemoveHealthLoss(gameConfiguration);
        Assert.That(gameConfiguration.Items.SelectMany(i => i.BasePowerUpAttributes), Has.None.Matches<ItemBasePowerUpDefinition>(p => p.TargetAttribute?.Id == Stats.HealthLossAfterHit.Id));

        await new HealthLossPerHitUpdatePlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await new HealthLossPerHitUpdatePlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertOnlyListedItemsLoseHealth(gameConfiguration);
    }

    /// <summary>
    /// Tests that the server start installs the loss once, and only on a database which is not yet up to date.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task StartupInstallerSetsTheHealthLossOnceAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using (var context = contextProvider.CreateNewContext())
        {
            var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
            RemoveHealthLoss(gameConfiguration);
            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.HealthLossPerHit).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await HealthLossPerHitInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        Assert.That(await HealthLossPerHitInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);

        using var reloaded = contextProvider.CreateNewContext();
        var persisted = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        AssertOnlyListedItemsLoseHealth(persisted);
    }

    private static void RemoveHealthLoss(GameConfiguration gameConfiguration)
    {
        foreach (var item in gameConfiguration.Items)
        {
            foreach (var powerUp in item.BasePowerUpAttributes.Where(p => p.TargetAttribute?.Id == Stats.HealthLossAfterHit.Id).ToList())
            {
                item.BasePowerUpAttributes.Remove(powerUp);
            }
        }
    }

    private static void AssertOnlyListedItemsLoseHealth(GameConfiguration gameConfiguration)
    {
        var expected = HealthLossPerHit.Items.ToDictionary(i => (i.Group, i.Number), i => i.Health);
        foreach (var item in gameConfiguration.Items)
        {
            var powerUps = item.BasePowerUpAttributes.Where(p => p.TargetAttribute?.Id == Stats.HealthLossAfterHit.Id).ToList();
            if (expected.TryGetValue((item.Group, (short)item.Number), out var health))
            {
                Assert.That(powerUps, Has.Count.EqualTo(1), $"{item.Name} must have exactly one health loss");
                Assert.That(powerUps[0].BaseValue, Is.EqualTo(health), item.Name);
                Assert.That(powerUps[0].AggregateType, Is.EqualTo(AggregateType.AddRaw), item.Name);
            }
            else
            {
                Assert.That(powerUps, Is.Empty, $"{item.Name} must not lose health");
            }
        }

        Assert.That(expected.Keys.All(k => gameConfiguration.Items.Any(i => i.Group == k.Group && i.Number == k.Number)), Is.True, "every listed item exists");
    }
}
