// <copyright file="ArchangelWeaponSpeedTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the attack speed of the Divine Archangel weapons and the Absolute Scepter.
/// </summary>
[TestFixture]
internal class ArchangelWeaponSpeedTest
{
    /// <summary>
    /// Tests that a new season 6 database gives the six weapons attack speed 50.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasSpeed50Async()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        Assert.That(ArchangelWeaponSpeedUpdatePlugIn.Weapons, Has.Count.EqualTo(6));
        foreach (var (group, number) in ArchangelWeaponSpeedUpdatePlugIn.Weapons)
        {
            Assert.That(GetSpeed(gameConfiguration, group, number), Is.EqualTo(50f), $"Item ({group},{number})");
        }
    }

    /// <summary>
    /// Tests that the update changes only the six weapons in an existing database.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateSetsSpeed50OnlyOnTheSixWeaponsAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        var others = gameConfiguration.Items
            .Where(i => !ArchangelWeaponSpeedUpdatePlugIn.Weapons.Contains((i.Group, (short)i.Number)))
            .Select(i => (Item: i, Speed: GetSpeed(i)))
            .Where(x => x.Speed is not null)
            .ToList();
        foreach (var (group, number) in ArchangelWeaponSpeedUpdatePlugIn.Weapons)
        {
            SetSpeed(gameConfiguration, group, number, 30f);
        }

        await new ArchangelWeaponSpeedUpdatePlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        foreach (var (group, number) in ArchangelWeaponSpeedUpdatePlugIn.Weapons)
        {
            Assert.That(GetSpeed(gameConfiguration, group, number), Is.EqualTo(50f), $"Item ({group},{number})");
        }

        Assert.That(others, Is.Not.Empty);
        foreach (var (item, speed) in others)
        {
            Assert.That(GetSpeed(item), Is.EqualTo(speed), $"{item.Name} must stay unchanged");
        }
    }

    /// <summary>
    /// Tests that the server start installs the speed once, and only on a database which is not yet up to date.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task StartupInstallerSetsSpeedOnceAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using (var context = contextProvider.CreateNewContext())
        {
            var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
            foreach (var (group, number) in ArchangelWeaponSpeedUpdatePlugIn.Weapons)
            {
                SetSpeed(gameConfiguration, group, number, 30f);
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.ArchangelWeaponSpeed).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await ArchangelWeaponSpeedUpdateInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        Assert.That(await ArchangelWeaponSpeedUpdateInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);

        using var reloaded = contextProvider.CreateNewContext();
        var persisted = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        foreach (var (group, number) in ArchangelWeaponSpeedUpdatePlugIn.Weapons)
        {
            Assert.That(GetSpeed(persisted, group, number), Is.EqualTo(50f), $"Item ({group},{number})");
        }
    }

    private static float? GetSpeed(ItemDefinition item)
    {
        return item.BasePowerUpAttributes.SingleOrDefault(p => p.TargetAttribute?.Id == Stats.AttackSpeedByWeapon.Id)?.BaseValue;
    }

    private static float GetSpeed(GameConfiguration gameConfiguration, byte group, short number)
    {
        var item = gameConfiguration.Items.Single(i => i.Group == group && i.Number == number);
        return GetSpeed(item) ?? throw new InvalidOperationException($"Item ({group},{number}) has no attack speed.");
    }

    private static void SetSpeed(GameConfiguration gameConfiguration, byte group, short number, float speed)
    {
        var item = gameConfiguration.Items.Single(i => i.Group == group && i.Number == number);
        item.BasePowerUpAttributes.Single(p => p.TargetAttribute?.Id == Stats.AttackSpeedByWeapon.Id).BaseValue = speed;
    }
}
