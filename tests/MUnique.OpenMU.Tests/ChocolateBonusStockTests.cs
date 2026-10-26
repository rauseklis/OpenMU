// <copyright file="ChocolateBonusStockTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Verifies test-stock availability and intentionally empty Chocolate openings.
/// </summary>
[TestFixture]
public class ChocolateBonusStockTests
{
    [Test]
    public async Task UpgradePreservesExistingStockAndPinkAndLaterEditsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var store = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        foreach (var listing in store.Items.Where(item => item.Definition is { Group: 14, Number: 11 } && item.Level == 1
            || item.Definition is { Group: 14, Number: 32 or 33 or 34 or 84 }).ToArray())
        {
            store.Items.Remove(listing);
            await context.DeleteAsync(listing).ConfigureAwait(false);
        }

        var original = store.Items.Select(item => (item, item.ItemSlot, item.Level, item.Durability, item.StorePrice)).ToArray();
        var pink = configuration.Items.Single(item => item.Group == 14 && item.Number == 32);
        var pinkGroups = pink.DropItems.ToArray();
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(update => update.Version == (int)UpdateVersion.ChocolateBonusStock).ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await ChocolateBonusStockInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(store.Items.Count, Is.EqualTo(original.Length + 5));
        foreach (var (item, slot, level, durability, price) in original)
        {
            Assert.That(store.Items, Does.Contain(item));
            Assert.That((item.ItemSlot, item.Level, item.Durability, item.StorePrice), Is.EqualTo((slot, level, durability, price)));
        }

        Assert.That(pink.DropItems, Is.EqualTo(pinkGroups));
        var star = store.Items.Single(item => item.Definition is { Group: 14, Number: 11 } && item.Level == 1);
        star.StorePrice = 4321;
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await ChocolateBonusStockInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        Assert.That(star.StorePrice, Is.EqualTo(4321));
    }

    [Test]
    public async Task StockInitializerOffersReviewedBonusesAndEmptyRedBlueAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        new MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items.ChocolateBonusStock(context, configuration).Initialize();
        var store = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        foreach (var level in new byte[] { 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12 })
        {
            var listing = store.Items.SingleOrDefault(item => item.Definition is { Group: 14, Number: 11 } && item.Level == level);
            Assert.That(listing, Is.Not.Null, $"Missing bonus source level {level}.");
            Assert.That(listing!.StorePrice, Is.EqualTo(1000));
        }

        foreach (var number in new short[] { 32, 33, 34, 84 })
        {
            var listing = store.Items.SingleOrDefault(item => item.Definition is { Group: 14 } definition && definition.Number == number && item.Level == 0);
            Assert.That(listing, Is.Not.Null, $"Missing box 14/{number}.");
            Assert.That(listing!.StorePrice, Is.EqualTo(1000));
        }

        var generator = new DefaultDropGenerator(configuration, Rand.GetRandomizer());
        foreach (var number in new short[] { 33, 34 })
        {
            var groups = configuration.Items.Single(item => item.Group == 14 && item.Number == number).DropItems;
            Assert.That(groups, Has.Count.EqualTo(1), "Keep an opening group so the box is consumed rather than dropped as an ordinary item.");
            Assert.That(groups.Single().PossibleItems, Is.Empty);
            for (var draw = 0; draw < 50; draw++)
            {
                var result = generator.GenerateItemDrop(groups);
                Assert.That(result.Item, Is.Null);
                Assert.That(result.Money, Is.Null, "No Zen outcome, including no zero-Zen object.");
            }
        }

        Assert.That(configuration.Items.Single(item => item.Group == 14 && item.Number == 32).DropItems.Any(group => group.PossibleItems.Count > 0), Is.True, "Pink remains available for the next reward design.");
    }
}
