// <copyright file="LorenciaShopLayoutTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
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

        var star = store.Items.Single(item => item.Definition is { Group: 14, Number: 11 } && item.Level == 1);
        star.ItemSlot = 119;
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await LorenciaShopLayoutInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        Assert.That(star.ItemSlot, Is.EqualTo(119));
    }

    internal static void AssertValidLayout(GameConfiguration configuration)
    {
        var store = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        var top = store.Items.Where(item => item.ItemSlot < 8).OrderBy(item => item.ItemSlot)
            .Select(item => ((int)item.Definition!.Group, (int)item.Definition.Number, (int)item.Level)).ToArray();
        Assert.That(top, Is.EqualTo(new[] { (14, 11, 5), (14, 11, 6), (14, 11, 3), (14, 11, 2), (14, 11, 1), (14, 32, 0), (14, 34, 0), (14, 33, 0) }));
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

        Assert.That(store.Items.Where(item => item.ItemSlot is >= 16 and < 24)
            .OrderBy(item => item.ItemSlot).Select(item => ((int)item.Definition!.Group, (int)item.Definition.Number, (int)item.Level)),
            Is.EqualTo(new[] { (12, 33, 0), (12, 34, 0), (12, 32, 0) }));
        Assert.That(store.Items.Where(item => item.ItemSlot is >= 8 and < 16).OrderBy(item => item.ItemSlot)
            .Select(item => ((int)item.Definition!.Group, (int)item.Definition.Number, (int)item.Level)),
            Is.EqualTo(new[] { (14, 11, 7), (14, 11, 8), (14, 11, 9), (14, 11, 10), (14, 11, 11), (14, 11, 12) }));
        Assert.That(store.Items.Where(item => item.ItemSlot is >= 24 and < 32)
            .OrderBy(item => item.ItemSlot).Select(item => ((int)item.Definition!.Group, (int)item.Definition.Number, (int)item.Level)),
            Is.EqualTo(new[] { (14, 11, 0), (14, 124, 0), (14, 123, 0) }));
    }
}
