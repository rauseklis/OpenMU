// <copyright file="ShopPotionStacksTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;

using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Verifies the potion stack size and the potion amounts which merchants sell.
/// </summary>
[TestFixture]
public class ShopPotionStacksTests
{
    /// <summary>
    /// A new database stacks potions to 50 and sells each potion only as +0 x1, x10 and x25.
    /// </summary>
    [Test]
    public async Task NewDatabaseSellsPotionsInThreeAmounts()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        AssertStacksAndShops(configuration);
    }

    /// <summary>
    /// The update fixes an existing database exactly once.
    /// </summary>
    [Test]
    public async Task StartupInstallerFixesAnExistingDatabaseOnce()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        foreach (var definition in configuration.Items.Where(ShopPotionStacks.IsPotion))
        {
            definition.Durability = 3;
        }

        var girl = configuration.Monsters.Single(monster => monster.Number == 231).MerchantStore!;
        foreach (var listing in girl.Items.Where(item => item.Definition is { Group: 14 }).ToList())
        {
            girl.Items.Remove(listing);
        }

        var apple = configuration.Items.Single(item => item.Group == 14 && item.Number == 0);
        var heal = configuration.Items.Single(item => item.Group == 14 && item.Number == 1);
        AddListing(context, girl, apple, 0, 1, 0);
        AddListing(context, girl, apple, 8, 3, 0);
        AddListing(context, girl, apple, 16, 1, 1);
        AddListing(context, girl, apple, 20, 3, 1);
        AddListing(context, girl, heal, 1, 1, 0);
        AddListing(context, girl, heal, 9, 3, 0);
        AddListing(context, girl, heal, 17, 3, 1);
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.ShopPotionStacks)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await ShopPotionStacksInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        AssertStacksAndShops(configuration);
        Assert.That(
            girl.Items.Where(item => item.Definition is { Group: 14, Number: 0 or 1 }).Count(),
            Is.EqualTo(6),
            "Only apple and healing potion were sold there.");
        Assert.That(await ShopPotionStacksInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
    }

    private static void AddListing(IContext context, ItemStorage store, ItemDefinition definition, byte slot, byte stack, byte level)
    {
        var listing = context.CreateNew<Item>();
        listing.Definition = definition;
        listing.ItemSlot = slot;
        listing.Durability = stack;
        listing.Level = level;
        store.Items.Add(listing);
    }

    private static void AssertStacksAndShops(GameConfiguration configuration)
    {
        var potionDefinitions = configuration.Items.Where(ShopPotionStacks.IsPotion).ToList();
        Assert.That(potionDefinitions, Has.Count.EqualTo(14));
        Assert.That(potionDefinitions.Select(definition => definition.Durability), Is.All.EqualTo(50));

        foreach (var store in configuration.Monsters.Select(monster => monster.MerchantStore).OfType<ItemStorage>())
        {
            var potions = store.Items.Where(item => item.Definition is { } definition && ShopPotionStacks.IsPotion(definition)).ToList();
            Assert.That(potions.Select(item => item.Level), Is.All.EqualTo(0));
            foreach (var group in potions.GroupBy(item => item.Definition))
            {
                Assert.That(group.Select(item => (int)item.Durability).Order(), Is.EqualTo(new[] { 1, 10, 25 }), group.Key!.Name);
            }

            Assert.That(potions.Select(item => item.ItemSlot), Is.Unique);
            Assert.That(store.Items.Except(potions).Select(item => item.ItemSlot).Intersect(potions.Select(item => item.ItemSlot)), Is.Empty);
        }

        var sellers = configuration.Monsters.Select(monster => monster.MerchantStore).OfType<ItemStorage>()
            .Count(store => store.Items.Any(item => item.Definition is { Group: 14, Number: 3 }));
        Assert.That(sellers, Is.GreaterThan(0));
    }
}
