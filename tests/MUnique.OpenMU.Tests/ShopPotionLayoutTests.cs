// <copyright file="ShopPotionLayoutTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Verifies that the potions of the shops sit in clean aligned rows.
/// </summary>
[TestFixture]
public class ShopPotionLayoutTests
{
    private static readonly byte[] ExpectedTypes = [0, 1, 2, 3, 4, 5, 6, 8];

    /// <summary>
    /// In a new database each potion type has its own column; row 0 holds single potions, row 1 stacks of 10, row 2 stacks of 25.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasAlignedPotionRowsAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        AssertAligned(configuration);
    }

    /// <summary>
    /// The update re-lays shops which have the potions in scattered slots, and does so only once.
    /// </summary>
    [Test]
    public async Task UpdateRealignsScatteredPotionsOnceAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var girl = configuration.Monsters.Single(monster => monster.Number == 231).MerchantStore!;
        var slot = 40;
        foreach (var potion in girl.Items.Where(item => item.Definition is { Group: 14 }).ToList())
        {
            potion.ItemSlot = (byte)slot++;
        }

        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.ShopPotionLayout)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await ShopPotionLayoutInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        AssertAligned(configuration);
        Assert.That(await ShopPotionLayoutInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
    }

    private static void AssertAligned(GameConfiguration configuration)
    {
        var shops = 0;
        foreach (var store in configuration.Monsters.Select(monster => monster.MerchantStore).OfType<ItemStorage>().Distinct())
        {
            var potions = store.Items.Where(item => item.Definition is { } definition && ShopPotionStacks.IsPotion(definition)).ToList();
            if (potions.Count == 0)
            {
                continue;
            }

            shops++;
            Assert.That(potions, Has.Count.EqualTo(ExpectedTypes.Length * 3));
            foreach (var potion in potions)
            {
                var column = Array.IndexOf(ExpectedTypes, (byte)potion.Definition!.Number);
                var row = Array.IndexOf(ShopPotionStacks.StackSizes, (byte)potion.Durability);
                Assert.That(column, Is.GreaterThanOrEqualTo(0), potion.Definition.Name);
                Assert.That(row, Is.GreaterThanOrEqualTo(0), potion.Definition.Name);
                Assert.That(potion.ItemSlot, Is.EqualTo((row * 8) + column), $"{potion.Definition.Name} x{potion.Durability}");
            }

            var others = store.Items.Except(potions).Select(item => (int)item.ItemSlot);
            Assert.That(others.Where(slot => slot < 24), Is.Empty, "No other item sits in the three potion rows.");
        }

        Assert.That(shops, Is.GreaterThanOrEqualTo(3), "Potion Girl, Elf Lala and Izabel sell potions.");
    }
}
