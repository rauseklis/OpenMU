// <copyright file="RibbonBoxShopTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Verifies the ribbon box listings on a fresh, disposable configuration.
/// </summary>
[TestFixture]
public class RibbonBoxShopTests
{
    /// <summary>
    /// Each ribbon box is sold by Lumen for exactly 1,000 Zen.
    /// </summary>
    [Test]
    public async Task LumenSellsAllRibbonBoxesForOneThousandZenAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var store = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        var originalStock = store.Items.ToArray();
        new RibbonBoxShop(context, configuration).Initialize();
        Assert.That(store.Items, Is.EquivalentTo(originalStock), "Repeated initialization must not duplicate listings.");
        foreach (var number in new short[] { 32, 33, 34 })
        {
            var listings = store.Items.Where(item => item.Definition is { Group: 12 } definition && definition.Number == number && item.Level == 0).ToArray();
            Assert.That(listings, Has.Length.EqualTo(1), $"Ribbon box 12:{number} must be listed exactly once.");
            Assert.That(new ItemPriceCalculator().CalculateMerchantBuyingPrice(listings.Single()), Is.EqualTo(1000));
        }
    }

    /// <summary>
    /// Upgrading existing stock preserves every previous listing and avoids duplicates.
    /// </summary>
    [Test]
    public async Task UpgradePreservesExistingStockAndRunsOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var store = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
            foreach (var listing in store.Items.Where(item => item.Definition is { Group: 12, Number: >= 32 and <= 34 }).ToArray())
            {
                store.Items.Remove(listing);
                await context.DeleteAsync(listing).ConfigureAwait(false);
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(update => update.Version == (int)UpdateVersion.RibbonBoxShop))
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            var originalStock = store.Items.Select(item => (item.Definition, item.ItemSlot, item.StorePrice)).ToArray();
            await context.SaveChangesAsync().ConfigureAwait(false);
            Assert.That(await RibbonBoxShopUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
            Assert.That(await RibbonBoxShopUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
            Assert.That(originalStock.All(original => store.Items.Any(item => (item.Definition, item.ItemSlot, item.StorePrice) == original)), Is.True);
            Assert.That(store.Items.Count, Is.EqualTo(originalStock.Length + 3));
        }
    }
}
