// <copyright file="LorenciaBarCustomItemsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Verifies that Lumen the Barmaid sells the Invisible set and the Mace of King.
/// </summary>
[TestFixture]
public class LorenciaBarCustomItemsTests
{
    private static readonly (byte Group, short Number, byte Slot, long Price)[] Expected =
    [
        (7, 74, 99, 175_100),
        (9, 74, 101, 175_100),
        (10, 74, 19, 175_100),
        (11, 74, 21, 175_100),
        (8, 74, 96, 175_100),
        (2, 22, 98, 82_100),
    ];

    /// <summary>
    /// A new database lists the six items at +0 in aligned, non-overlapping slots.
    /// </summary>
    [Test]
    public async Task NewDatabaseListsTheItemsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);

        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        AssertListed(configuration);
    }

    /// <summary>
    /// The update adds the items to an existing database once and does not change the other stock.
    /// </summary>
    [Test]
    public async Task UpdateAddsTheItemsOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);

        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var store = LumenStore(configuration);
        var otherStock = store.Items.Where(item => !IsListed(item)).Select(item => (item.Definition, item.ItemSlot)).ToList();
        foreach (var listing in store.Items.Where(IsListed).ToList())
        {
            store.Items.Remove(listing);
        }

        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.LorenciaBarCustomItems)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await LorenciaBarCustomItemsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        AssertListed(configuration);
        Assert.That(store.Items.Where(item => !IsListed(item)).Select(item => (item.Definition, item.ItemSlot)), Is.EquivalentTo(otherStock));
        Assert.That(await LorenciaBarCustomItemsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
    }

    private static ItemStorage LumenStore(GameConfiguration configuration)
        => configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;

    private static bool IsListed(Item item)
        => item.Definition is { } definition && Expected.Any(entry => entry.Group == definition.Group && entry.Number == definition.Number);

    private static void AssertListed(GameConfiguration configuration)
    {
        var store = LumenStore(configuration);
        var calculator = new ItemPriceCalculator();
        foreach (var (group, number, slot, price) in Expected)
        {
            var listing = store.Items.Single(item => item.Definition!.Group == group && item.Definition.Number == number);
            Assert.That(listing.ItemSlot, Is.EqualTo(slot), listing.Definition!.Name.ToString());
            Assert.That(listing.Level, Is.EqualTo(0));
            Assert.That(listing.StorePrice, Is.Null);
            Assert.That(calculator.CalculateMerchantBuyingPrice(listing), Is.EqualTo(price), listing.Definition.Name.ToString());
        }

        var occupied = new HashSet<int>();
        foreach (var item in store.Items)
        {
            for (var y = 0; y < item.Definition!.Height; y++)
            {
                for (var x = 0; x < item.Definition.Width; x++)
                {
                    Assert.That((item.ItemSlot % 8) + x, Is.LessThan(8), "inside the shop width");
                    Assert.That(occupied.Add(item.ItemSlot + (y * 8) + x), Is.True, $"{item.Definition.Name} overlaps another item");
                }
            }

            Assert.That((item.ItemSlot / 8) + item.Definition.Height, Is.LessThanOrEqualTo(15), "inside the shop height");
        }
    }
}
