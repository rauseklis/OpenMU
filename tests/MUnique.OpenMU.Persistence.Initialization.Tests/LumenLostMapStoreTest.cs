// <copyright file="LumenLostMapStoreTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the Lost Maps sold by Lumen the Barmaid in the Season 6 configuration.
/// </summary>
[TestFixture]
internal class LumenLostMapStoreTest
{
    private const short LumenNpcNumber = 255;
    private const byte LostMapGroup = 14;
    private const byte LostMapNumber = 28;

    /// <summary>
    /// Tests that a new Season 6 database contains every Lost Map exactly once in Lumen's store.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsAllLostMapsAsync()
    {
        var (_, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);

        AssertLostMaps(GetLumenStore(gameConfiguration));
    }

    /// <summary>
    /// Tests that the update restores the maps to an existing store and remains idempotent.
    /// </summary>
    [Test]
    public async Task UpdateAddsAllLostMapsOnceAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using var _ = context;
        var store = GetLumenStore(gameConfiguration);
        var existingItemCount = store.Items.Count;
        foreach (var lostMap in GetLostMaps(store).ToList())
        {
            store.Items.Remove(lostMap);
        }

        var update = new AddLostMapsToLumenStorePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertLostMaps(store);
        Assert.That(store.Items, Has.Count.EqualTo(existingItemCount));
    }

    private static void AssertLostMaps(ItemStorage store)
    {
        var lostMaps = GetLostMaps(store).OrderBy(item => item.Level).ToList();
        Assert.That(lostMaps.Select(item => item.Level), Is.EqualTo(Enumerable.Range(1, 7)));
        Assert.That(lostMaps.Select(item => item.ItemSlot), Is.EqualTo(Enumerable.Range(72, 7)));
    }

    private static IEnumerable<Item> GetLostMaps(ItemStorage store)
    {
        return store.Items.Where(item => item.Definition is { Group: LostMapGroup, Number: LostMapNumber });
    }

    private static ItemStorage GetLumenStore(GameConfiguration gameConfiguration)
    {
        return gameConfiguration.Monsters.Single(monster => monster.Number == LumenNpcNumber).MerchantStore!;
    }

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        return (context, gameConfiguration);
    }
}
