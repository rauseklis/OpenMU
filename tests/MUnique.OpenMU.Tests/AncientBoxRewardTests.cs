// <copyright file="AncientBoxRewardTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Verifies that Ancient box rewards remain inside their explicitly configured pool.
/// </summary>
[TestFixture]
public class AncientBoxRewardTests
{
    /// <summary>
    /// Verifies every configured piece, its variant and the exact Ancient bonus boundary.
    /// </summary>
    [Test]
    public async Task TierRewardsPreserveVariantsAndBonusWeightsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        foreach (var tier in new[] { (Number: 11, Pieces: 11), (Number: 124, Pieces: 29), (Number: 123, Pieces: 28) })
        {
            var box = config.Items.Single(item => item.Group == 14 && item.Number == tier.Number);
            var groups = box.DropItems.Where(group => group.SourceItemLevel == 0).ToList();
            Assert.That(groups.Sum(group => group.PossibleItems.Count), Is.EqualTo(tier.Pieces));
            Assert.That(groups.Sum(group => group.Chance), Is.EqualTo(1).Within(0.000000001));
            foreach (var group in groups)
            {
                Assert.That(group.Chance / group.PossibleItems.Count, Is.EqualTo(1d / tier.Pieces).Within(0.000000001));
                Assert.That(group.AncientSet, Is.Not.Null);
                Assert.That(group.AncientSet!.Name.ToString(), Is.Not.EqualTo("Rave"));
                foreach (var member in group.AncientSet.Items)
                {
                    foreach (var roll in new[] { 0.299999, 0.30, 0.99 })
                    {
                        var randomizer = new Mock<IRandomizer>();
                        var memberIndex = group.PossibleItems.ToList().IndexOf(member.ItemDefinition!);
                        randomizer.Setup(random => random.NextInt(It.IsAny<int>(), It.IsAny<int>()))
                            .Returns((int minimum, int maximum) => minimum == 0 && maximum == group.PossibleItems.Count ? memberIndex : minimum);
                        randomizer.Setup(random => random.NextDouble()).Returns(roll);
                        var item = new DefaultDropGenerator(config, randomizer.Object).GenerateItemDrop(group);
                        Assert.That(item, Is.Not.Null);
                        Assert.That(item!.Definition, Is.SameAs(member.ItemDefinition));
                        Assert.That(item.Level, Is.Zero);
                        Assert.That(item.ItemSetGroups.Single(), Is.SameAs(member));
                        Assert.That(item.ItemOptions.Single(option => option.ItemOption?.OptionType == ItemOptionTypes.AncientBonus).Level,
                            Is.EqualTo(roll < 0.30 ? 2 : 1));
                        Assert.That(item.HasSkill, Is.EqualTo(item.CanHaveSkill()));
                        Assert.That(item.SocketCount, Is.Zero);
                    }
                }
            }
        }

        var before = config.Items.Single(item => item.Group == 14 && item.Number == 11).DropItems.Where(group => group.SourceItemLevel != 0).ToArray();
        new AncientRewardBoxes(context, config).Initialize();
        Assert.That(config.Items.Single(item => item.Group == 14 && item.Number == 11).DropItems.Where(group => group.SourceItemLevel != 0), Is.EqualTo(before));
        new TriteArmors(context, config).Initialize();
        new RageFighterAncientSets(context, config).Initialize();
        Assert.That(config.ItemSetGroups.Count(set => set.Name == "Chamer"), Is.EqualTo(1));
        Assert.That(config.ItemSetGroups.Count(set => set.Name == "Trite"), Is.EqualTo(1));
        var store = config.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        var stockCount = store.Items.Count;
        new AncientBoxShop(context, config).Initialize();
        Assert.That(store.Items.Count, Is.EqualTo(stockCount), "Repeated installation must not duplicate shop stock.");
        foreach (var number in new short[] { 11, 124, 123 })
        {
            var listing = store.Items.Single(item => item.Definition is { Group: 14 } definition && definition.Number == number && item.Level == 0);
            Assert.That(new ItemPriceCalculator().CalculateMerchantBuyingPrice(listing), Is.EqualTo(1000));
        }

        foreach (var item in config.Items.Where(item => item.Number == 72 && item.Group is 7 or 8 or 9 or 11))
        {
            Assert.That(item.IsTradable && item.IsDroppable && item.IsStorable && item.IsRepairable && item.IsPersonalStoreSellable, Is.True);
            Assert.That(item.DropsFromMonsters, Is.False);
            Assert.That(item.PossibleItemSetGroups.Count(set => set.AlwaysApplies), Is.EqualTo(7));
        }
    }

    /// <summary>
    /// An Ancient reward must not select an unrelated item from the global Ancient pool.
    /// </summary>
    [Test]
    public async Task ExplicitAncientPoolIsRespectedAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var expected = config.Items.Single(item => item.Group == 11 && item.Number == 2);
        var group = context.CreateNew<ItemDropItemGroup>();
        group.ItemType = SpecialItemType.Ancient;
        group.Chance = 1;
        group.PossibleItems.Add(expected);
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(random => random.NextInt(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((int minimum, int maximum) => minimum);
        randomizer.Setup(random => random.NextDouble()).Returns(0.99);

        var reward = new DefaultDropGenerator(config, randomizer.Object).GenerateItemDrop(group);

        Assert.That(reward, Is.Not.Null);
        Assert.That(reward!.Definition, Is.SameAs(expected));
        Assert.That(reward.ItemSetGroups.Single().ItemSetGroup?.Name.ToString(), Is.EqualTo("Barnake"));
    }
}
