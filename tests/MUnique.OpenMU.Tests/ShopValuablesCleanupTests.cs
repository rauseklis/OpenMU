// <copyright file="ShopValuablesCleanupTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Verifies that no merchant sells jewels, boxes or other valuables.
/// </summary>
[TestFixture]
public class ShopValuablesCleanupTests
{
    /// <summary>
    /// A new Season 6 database has no valuables in any shop, but keeps the normal stock.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasNoValuablesInAnyShop()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        var listings = StoreListings(configuration);

        Assert.That(listings.Where(item => ShopValuablesCleanup.IsValuable(item.Definition!)).Select(item => item.Definition!.Name), Is.Empty);
        Assert.That(listings.Any(item => item.Definition is { Group: 14, Number: 3 }), Is.True, "Large Healing Potion must still be sold.");
        Assert.That(listings.Any(item => item.Definition is { Group: 13, Number: 46 }), Is.True, "Devil Square Ticket must still be sold.");
    }

    /// <summary>
    /// The update removes valuables from an existing database exactly once.
    /// </summary>
    [Test]
    public async Task StartupInstallerCleansAnExistingDatabaseOnce()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var lumen = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        var box = configuration.Items.Single(item => item.Group == 14 && item.Number == 11);
        var listing = context.CreateNew<DataModel.Entities.Item>();
        listing.Definition = box;
        listing.Durability = 1;
        listing.ItemSlot = 119;
        lumen.Items.Add(listing);
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.RemoveShopValuables)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await RemoveShopValuablesInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        Assert.That(StoreListings(configuration).Any(item => ShopValuablesCleanup.IsValuable(item.Definition!)), Is.False);
        Assert.That(await RemoveShopValuablesInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
    }

    private static List<DataModel.Entities.Item> StoreListings(GameConfiguration configuration)
    {
        return configuration.Monsters.Where(monster => monster.MerchantStore is not null)
            .SelectMany(monster => monster.MerchantStore!.Items)
            .ToList();
    }
}
