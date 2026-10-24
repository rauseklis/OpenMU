// <copyright file="ItemRulesDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the item rule flags of the season 6 data (<see cref="ItemRules"/>).
/// </summary>
[TestFixture]
internal class ItemRulesDataTest
{
    private GameConfiguration _season6 = null!;

    /// <summary>
    /// Creates the season 6 data once for the tests which only read it.
    /// </summary>
    [OneTimeSetUp]
    public async Task CreateSeason6DataAsync()
    {
        this._season6 = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Tests that every listed item exists once in the season 6 data.
    /// </summary>
    [Test]
    public void EveryListedItemExists()
    {
        var gameConfiguration = this._season6;

        Assert.That(ItemRules.Rules.Select(rule => (rule.Group, rule.Number)), Is.Unique);
        foreach (var (group, number, _) in ItemRules.Rules)
        {
            Assert.That(gameConfiguration.Items.Count(item => item.Group == group && item.Number == number), Is.EqualTo(1), $"Item ({group},{number})");
        }
    }

    /// <summary>
    /// Tests that pets, mounts and quest items can't be dropped, while normal items still can.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    [TestCase((byte)13, (short)0)] // Guardian Angel
    [TestCase((byte)13, (short)1)] // Imp
    [TestCase((byte)13, (short)2)] // Horn of Uniria
    [TestCase((byte)13, (short)4)] // Dark Horse
    [TestCase((byte)13, (short)37)] // Horn of Fenrir
    [TestCase((byte)13, (short)123)] // Pet Skeleton
    [TestCase((byte)14, (short)102)] // Gaion's Order
    [TestCase((byte)14, (short)109)] // Complete Secromicon
    public void PetsAndQuestItemsCanNotBeDropped(byte group, short number)
    {
        Assert.That(GetItem(this._season6, group, number).IsDroppable, Is.False);
    }

    /// <summary>
    /// Tests that the update makes the pets undroppable in an existing database.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateMakesPetsUndroppableAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        foreach (var item in gameConfiguration.Items)
        {
            item.IsDroppable = true;
        }

        await new PetsAndQuestItemsNotDroppableUpdatePlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(GetItem(gameConfiguration, 13, 1).IsDroppable, Is.False);
        Assert.That(GetItem(gameConfiguration, 0, 0).IsDroppable, Is.True);
    }

    /// <summary>
    /// Tests that the server start installs the drop rules once, and only on a database which is not yet up to date.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task StartupInstallerMakesPetsUndroppableOnceAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using (var context = contextProvider.CreateNewContext())
        {
            var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
            foreach (var item in gameConfiguration.Items)
            {
                item.IsDroppable = true;
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.PetsAndQuestItemsNotDroppable).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await PetsAndQuestItemsNotDroppableUpdateInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        Assert.That(await PetsAndQuestItemsNotDroppableUpdateInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);

        using var reloaded = contextProvider.CreateNewContext();
        var persisted = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        Assert.That(GetItem(persisted, 13, 1).IsDroppable, Is.False);
        Assert.That(GetItem(persisted, 0, 0).IsDroppable, Is.True);
    }

    /// <summary>
    /// Tests that a new season 6 database contains the item rules.
    /// </summary>
    [Test]
    public void NewDatabaseContainsItemRules()
    {
        var gameConfiguration = this._season6;

        // Kris: a normal weapon allows everything.
        var kris = GetItem(gameConfiguration, 0, 0);
        Assert.That(new[] { kris.IsTradable, kris.IsDroppable, kris.IsStorable, kris.IsSellableToNpc, kris.IsPersonalStoreSellable, kris.IsRepairable }, Is.All.True);

        // Scroll of the Emperor: a quest item.
        var scroll = GetItem(gameConfiguration, 14, 23);
        Assert.That(scroll.IsTradable, Is.False);
        Assert.That(scroll.IsDroppable, Is.False);
        Assert.That(scroll.IsStorable, Is.False);
        Assert.That(scroll.IsPersonalStoreSellable, Is.False);
        Assert.That(scroll.IsRepairable, Is.False);

        // Potions can't be repaired: their durability is the number of pieces.
        Assert.That(GetItem(gameConfiguration, 14, 3).IsRepairable, Is.False);

        // The Wizards Ring is bound to the character, and its flags follow that.
        var wizardsRing = GetItem(gameConfiguration, 13, 20);
        Assert.That(wizardsRing.IsTradable, Is.False);
        Assert.That(wizardsRing.IsStorable, Is.False);
        Assert.That(wizardsRing.IsPersonalStoreSellable, Is.False);

        // The trainable pets stay repairable on the server.
        Assert.That(GetItem(gameConfiguration, 13, 4).IsRepairable, Is.True);
        Assert.That(GetItem(gameConfiguration, 13, 5).IsRepairable, Is.True);
    }

    /// <summary>
    /// Tests that the update sets the same rules on a database which was created before they existed.
    /// </summary>
    [Test]
    public async Task UpdateSetsItemRulesOnExistingDatabaseAsync()
    {
        var expected = this._season6;

        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        // The state after the database migration: every item allows everything.
        foreach (var item in gameConfiguration.Items)
        {
            item.IsTradable = item.IsDroppable = item.IsStorable = item.IsSellableToNpc = item.IsPersonalStoreSellable = item.IsRepairable = true;
        }

        await new AddItemRuleFlagsPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        foreach (var item in gameConfiguration.Items)
        {
            var expectedItem = GetItem(expected, item.Group, item.Number);
            Assert.That(Flags(item), Is.EqualTo(Flags(expectedItem)), $"Item ({item.Group},{item.Number})");
        }
    }

    private static (bool, bool, bool, bool, bool, bool) Flags(ItemDefinition item)
    {
        return (item.IsTradable, item.IsDroppable, item.IsStorable, item.IsSellableToNpc, item.IsPersonalStoreSellable, item.IsRepairable);
    }

    private static ItemDefinition GetItem(GameConfiguration gameConfiguration, byte group, short number)
    {
        return gameConfiguration.Items.Single(item => item.Group == group && item.Number == number);
    }

    private static async Task<GameConfiguration> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
