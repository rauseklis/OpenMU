// <copyright file="CherryBlossomRewardTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Pins the approved Cherry Blossom drops and exchange pools.
/// </summary>
[TestFixture]
public class CherryBlossomRewardTests
{
    [Test]
    public async Task PlayBoxContainsSixEqualOutcomesAndSingleBranchesAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var box = configuration.Items.Single(item => item.Group == 14 && item.Number == 84);
        var groups = box.DropItems.Where(group => group.SourceItemLevel == 0).ToArray();
        Assert.That(groups, Has.Length.EqualTo(1));
        var group = groups.Single();
        Assert.That(group.Chance, Is.EqualTo(1));
        Assert.That(group.PossibleItems.Select(item => item.Number), Is.EquivalentTo(new short[] { 85, 86, 87, 88, 89, 90 }));
        var random = new Mock<IRandomizer>();
        random.Setup(value => value.NextDouble()).Returns(0.99);
        foreach (var number in new short[] { 88, 89, 90 })
        {
            var index = group.PossibleItems.ToList().FindIndex(item => item.Number == number);
            random.Setup(value => value.NextInt(0, 6)).Returns(index);
            var item = new DefaultDropGenerator(configuration, random.Object).GenerateItemDrop(group);
            Assert.That(item, Is.Not.Null);
            Assert.That(item!.Definition!.Number, Is.EqualTo(number));
            Assert.That(item.Durability, Is.EqualTo(1), "A Play-Box must drop one branch, not a full stack.");
        }
    }

    [TestCase(88, 10)]
    [TestCase(89, 30)]
    [TestCase(90, 255)]
    public async Task ExchangeUsesExactBranchCountAndApprovedPoolAsync(short branchNumber, int required)
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var npc = configuration.Monsters.Single(monster => monster.Number == 450);
        var recipe = npc.ItemCraftings.SingleOrDefault(crafting => crafting.SimpleCraftingSettings!.RequiredItems
            .Any(input => input.PossibleItems.Any(item => item.Group == 14 && item.Number == branchNumber)));
        Assert.That(recipe, Is.Not.Null, "Every branch color needs an exchange recipe.");
        var settings = recipe!.SimpleCraftingSettings!;
        Assert.That(settings.SuccessPercent, Is.EqualTo(100));
        Assert.That(settings.Money, Is.Zero);
        var input = settings.RequiredItems.Single();
        Assert.That(input.MinimumAmount, Is.EqualTo(required));
        Assert.That(input.MaximumAmount, Is.EqualTo(required));
        var results = settings.ResultItems.ToArray();
        var expected = branchNumber switch
        {
            88 => new[] { (14, 13), (12, 15), (14, 16), (14, 14), (13, 48), (13, 65) },
            89 => new[] { (14, 11), (13, 46), (13, 47), (13, 123), (13, 106), (13, 80) },
            _ => new[] { (13, 47), (13, 46), (12, 30), (12, 31), (14, 124), (14, 123) },
        };
        Assert.That(results.Select(result => ((int)result.ItemDefinition!.Group, (int)result.ItemDefinition.Number)).Distinct(), Is.EquivalentTo(expected));
        foreach (var outcome in results.GroupBy(result => (result.ItemDefinition!.Group, result.ItemDefinition.Number)))
        {
            var expectedChance = branchNumber != 90 ? 1.0 / 6 : outcome.Key == ((byte)14, (short)123) ? 0.10 : 0.18;
            Assert.That((double)outcome.Count() / results.Length, Is.EqualTo(expectedChance).Within(0.000000001));
        }

        Assert.That(results.Where(result => result.ItemDefinition!.Group == 13 && result.ItemDefinition.Number is >= 46 and <= 48)
            .All(result => result.Durability == (branchNumber == 90 ? 10 : 1)), Is.True);
        Assert.That(results.All(result => result.RandomMinimumLevel == 0 && result.RandomMaximumLevel == 0), Is.True);
    }
    [TestCase(88, 10)]
    [TestCase(89, 30)]
    [TestCase(90, 255)]
    public async Task ExchangesRejectWrongCountsAndConsumeExactlyOnceAsync(short number, int count)
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var settings = configuration.Monsters.Single(monster => monster.Number == 450).ItemCraftings
            .Single(recipe => recipe.SimpleCraftingSettings!.RequiredItems.Any(input => input.PossibleItems.Any(item => item.Number == number && item.Group == 14)))
            .SimpleCraftingSettings!;
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        using var playerContext = player.PersistenceContext;
        var branch = playerContext.CreateNew<MUnique.OpenMU.DataModel.Entities.Item>();
        branch.Definition = configuration.Items.Single(item => item.Group == 14 && item.Number == number);
        branch.Durability = count - 1;
        Assert.That(await player.TemporaryStorage!.AddItemAsync(branch).ConfigureAwait(false), Is.True);
        var handler = new SimpleItemCraftingHandler(settings);
        Assert.That(handler.TryGetRequiredItems(player, out _, out _), Is.EqualTo(CraftingResult.LackingMixItems));
        branch.Durability = count + 1;
        Assert.That(handler.TryGetRequiredItems(player, out _, out _), Is.EqualTo(CraftingResult.TooManyItems));
        branch.Durability = count;
        var (result, reward) = await handler.DoMixAsync(player, 0).ConfigureAwait(false);
        Assert.That(result, Is.EqualTo(CraftingResult.Success));
        Assert.That(reward, Is.Not.Null);
        Assert.That(player.TemporaryStorage.Items, Has.Exactly(1).Items);
        Assert.That(player.TemporaryStorage.Items, Does.Not.Contain(branch));
        Assert.That(reward!.Durability, Is.GreaterThan(0));
        Assert.That(reward.Level, Is.Zero);
        Assert.That((await handler.DoMixAsync(player, 0).ConfigureAwait(false)).Result, Is.Not.EqualTo(CraftingResult.Success));
        Assert.That(player.TemporaryStorage.Items.Single(), Is.SameAs(reward));
    }

    [Test]
    public async Task ExistingConfigurationUpdateIsInstalledOnlyOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
            .Where(update => update.Version == (int)UpdateVersion.CherryBlossomRewards).ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var box = configuration.Items.Single(item => item.Group == 14 && item.Number == 84);
        var preserved = context.CreateNew<ItemDropItemGroup>();
        preserved.SourceItemLevel = 1;
        box.DropItems.Add(preserved);
        var unrelatedBox = configuration.Items.Single(item => item.Group == 14 && item.Number == 123);
        var oldRewards = unrelatedBox.DropItems.ToArray();
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await CherryBlossomRewardsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        var recipes = configuration.Monsters.Single(monster => monster.Number == 450).ItemCraftings.ToArray();
        Assert.That(await CherryBlossomRewardsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        Assert.That(configuration.Monsters.Single(monster => monster.Number == 450).ItemCraftings, Is.EquivalentTo(recipes));
        Assert.That(box.DropItems, Does.Contain(preserved));
        Assert.That(unrelatedBox.DropItems, Is.EquivalentTo(oldRewards));
    }

    [Test]
    public async Task CherryBlossomShopReplacesOnlyTicketMaterialsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var shop = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        Assert.That(shop.Items.Any(item => item.Definition is { Group: 13, Number: 16 or 17 }
            or { Group: 14, Number: 17 or 18 }), Is.False);
        foreach (var (number, count) in new[] { (88, 10), (89, 30), (90, 255) })
        {
            var branch = shop.Items.Single(item => item.Definition is { Group: 14 } definition && definition.Number == number);
            Assert.That(branch.Durability, Is.EqualTo(count));
            Assert.That(new ItemPriceCalculator().CalculateMerchantBuyingPrice(branch), Is.EqualTo(1000));
        }

        foreach (var number in new short[] { 11, 123, 124 })
        {
            Assert.That(shop.Items.Any(item => item.Definition is { Group: 14 } definition && definition.Number == number), Is.True);
        }
        foreach (var number in new short[] { 46, 47, 48 })
        {
            Assert.That(shop.Items.Any(item => item.Definition is { Group: 13 } definition && definition.Number == number), Is.True);
        }
    }

    [Test]
    public async Task EachConfiguredRewardIsDeliveredWithItsExactUsesAndHealthAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        foreach (var recipe in configuration.Monsters.Single(monster => monster.Number == 450).ItemCraftings)
        {
            var sourceSettings = recipe.SimpleCraftingSettings!;
            foreach (var result in sourceSettings.ResultItems.DistinctBy(result => result.ItemDefinition))
            {
                var settings = context.CreateNew<SimpleCraftingSettings>();
                settings.SuccessPercent = 100;
                settings.RequiredItems.Add(sourceSettings.RequiredItems.Single());
                settings.ResultItems.Add(result);
                var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
                using var playerContext = player.PersistenceContext;
                var branch = playerContext.CreateNew<MUnique.OpenMU.DataModel.Entities.Item>();
                branch.Definition = settings.RequiredItems.Single().PossibleItems.Single();
                branch.Durability = settings.RequiredItems.Single().MinimumAmount;
                await player.TemporaryStorage!.AddItemAsync(branch).ConfigureAwait(false);
                var outcome = await new SimpleItemCraftingHandler(settings).DoMixAsync(player, 0).ConfigureAwait(false);
                Assert.That(outcome.Result, Is.EqualTo(CraftingResult.Success));
                Assert.That(outcome.Item!.Definition, Is.SameAs(result.ItemDefinition));
                Assert.That(player.TemporaryStorage.Items.Count(), Is.EqualTo(1));
                var expectedDurability = result.Durability ?? (result.ItemDefinition!.Group == 13 && result.ItemDefinition.Number is 65 or 80 or 106 or 123 ? 255 : 1);
                Assert.That(outcome.Item.Durability, Is.EqualTo(expectedDurability));
                Assert.That(outcome.Item.Level, Is.Zero, "Level zero jewel packs contain ten jewels.");
            }
        }
    }

    [Test]
    public async Task ShopChangesPreserveUnrelatedStockAndLaterBranchEditsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var shop = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        var preserved = shop.Items.Select(item => (Item: item, item.ItemSlot, item.StorePrice, item.Durability)).ToArray();
        var material = context.CreateNew<MUnique.OpenMU.DataModel.Entities.Item>();
        material.Definition = configuration.Items.Single(item => item.Group == 13 && item.Number == 17);
        material.ItemSlot = 112;
        shop.Items.Add(material);
        var initializer = new MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items.CherryBlossomShop(context, configuration);
        initializer.Initialize();
        initializer.Initialize();
        Assert.That(shop.Items, Does.Not.Contain(material));
        Assert.That(shop.Items, Has.Count.EqualTo(preserved.Length));
        Assert.That(preserved.All(previous => shop.Items.Contains(previous.Item) && previous.Item.ItemSlot == previous.ItemSlot
            && previous.Item.StorePrice == previous.StorePrice && previous.Item.Durability == previous.Durability), Is.True);
    }

}
