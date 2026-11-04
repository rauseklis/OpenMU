// <copyright file="LorenciaShopLayoutTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Verifies horizontal progression rows and the reserved empty rows in Lumen's shop.
/// </summary>
[TestFixture]
public class LorenciaShopLayoutTests
{
    [Test]
    public async Task FreshShopUsesProgressionRowAndTwoEmptyHorizontalRowsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        AssertValidLayout(configuration);
    }

    [Test]
    public async Task UpgradeMovesOnlySlotsAndPreservesLaterLayoutEditsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var store = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        var original = store.Items.Select(item => (item, item.Definition, item.Level, item.Durability, item.StorePrice)).ToArray();
        foreach (var item in store.Items)
        {
            item.ItemSlot = (byte)(119 - item.ItemSlot);
        }

        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(update => update.Version == (int)UpdateVersion.LorenciaShopLayout).ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await LorenciaShopLayoutInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        AssertValidLayout(configuration);
        Assert.That(store.Items.Count, Is.EqualTo(original.Length));
        foreach (var (item, definition, level, durability, price) in original)
        {
            Assert.That(store.Items, Does.Contain(item));
            Assert.That((item.Definition, item.Level, item.Durability, item.StorePrice), Is.EqualTo((definition, level, durability, price)));
        }

        var scroll = store.Items.Single(item => item.Definition is { Group: 14, Number: 10 });
        scroll.ItemSlot = 119;
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await LorenciaShopLayoutInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        Assert.That(scroll.ItemSlot, Is.EqualTo(119));
    }

    internal static void AssertValidLayout(GameConfiguration configuration)
    {
        var store = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        var occupied = new HashSet<int>();
        foreach (var item in store.Items)
        {
            Assert.That(item.ItemSlot % 8 + item.Definition!.Width, Is.LessThanOrEqualTo(8));
            for (var row = 0; row < item.Definition.Height; row++)
            {
                for (var column = 0; column < item.Definition.Width; column++)
                {
                    var slot = item.ItemSlot + row * 8 + column;
                    Assert.That(slot, Is.LessThan(120));
                    Assert.That(slot is >= 32 and < 48, Is.False, "Horizontal rows five and six must stay empty.");
                    Assert.That(occupied.Add(slot), Is.True, "Shop items must not overlap.");
                }
            }
        }

        Assert.That(store.Items.Where(item => ShopValuablesCleanup.IsValuable(item.Definition!)), Is.Empty, "No box may be left in the layout rows.");
    }
}
