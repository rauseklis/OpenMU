// <copyright file="RibbonBoxRewardTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Pins the approved pools of ribbon box contents.
/// </summary>
[TestFixture]
public class RibbonBoxRewardTests
{
    /// <summary>
    /// Ribbon boxes guarantee exactly one reward from their approved pool at +0.
    /// </summary>
    [Test]
    public async Task RibbonBoxesContainOnlyApprovedRewardsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var expected = new Dictionary<short, (int Group, int Number)[]>
        {
            [32] = [(0, 19), (2, 13), (4, 18), (5, 10), (5, 36)],
            [33] = [(12, 30), (12, 31), (12, 136), (12, 141)],
            [34] = [(13, 8), (13, 9), (13, 12), (13, 13), (13, 21), (13, 22), (13, 23), (13, 25), (13, 26), (13, 27)],
        };
        foreach (var entry in expected)
        {
            var box = configuration.Items.Single(item => item.Group == 12 && item.Number == entry.Key);
            var groups = box.DropItems.Where(group => group.SourceItemLevel == 0).ToArray();
            Assert.That(groups, Has.Length.EqualTo(2));
            var group = groups.Single(group => group.ItemType != SpecialItemType.Money);
            var zen = groups.Single(group => group.ItemType == SpecialItemType.Money);
            Assert.That(zen.Chance + group.Chance, Is.EqualTo(1).Within(0.000000001));
            Assert.That(zen.MoneyAmount, Is.EqualTo(entry.Key switch { 32 => 10_000_000, 34 => 7_500_000, _ => 5_000_000 }));
            var randomizer = new Mock<IRandomizer>();
            randomizer.Setup(random => random.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns((int minimum, int maximum) => minimum);
            randomizer.Setup(random => random.NextDouble()).Returns(0.99);
            var generated = new DefaultDropGenerator(configuration, randomizer.Object).GenerateItemDrop(group);
            Assert.That(generated, Is.Not.Null);
            Assert.That(generated!.Level, Is.Zero);
            Assert.That(generated.HasSkill, Is.EqualTo(entry.Key == 32 && generated.CanHaveSkill()));
            Assert.That(group.Chance, Is.EqualTo(entry.Key switch { 32 => 0.10, 34 => 0.15, _ => 0.20 }));
            Assert.That(group.MinimumLevel, Is.Zero);
            Assert.That(group.MaximumLevel, Is.Zero);
            Assert.That(group.PossibleItems.Select(item => ((int)item.Group, (int)item.Number)), Is.EquivalentTo(entry.Value));
            foreach (var roll in new[] { group.Chance / 2, group.Chance + 0.000001, 0.99 })
            {
                randomizer.Setup(random => random.NextDouble()).Returns(roll);
                var outcome = new DefaultDropGenerator(configuration, randomizer.Object).GenerateItemDrop(groups);
                Assert.That(outcome.Item is not null, Is.EqualTo(roll < group.Chance));
                Assert.That(outcome.Money, roll < group.Chance ? Is.Null : Is.EqualTo((uint)zen.MoneyAmount));
            }
            if (entry.Key == 34)
            {
                Assert.That(group.ItemType, Is.EqualTo(SpecialItemType.Excellent));
                Assert.That(group.ExcellentSecondOptionChance, Is.EqualTo(0.15));
                Assert.That(group.GuaranteeJewelryOption, Is.True);
                var recovery = generated.ItemOptions.Single(link => link.ItemOption?.OptionType == ItemOptionTypes.Option);
                Assert.That(recovery.ItemOption!.PowerUpDefinition!.TargetAttribute, Is.EqualTo(Stats.HealthRecoveryMultiplier));
                Assert.That(recovery.Level, Is.InRange(1, 3));
            }

            if (entry.Key == 32)
            {
                Assert.That(group.LuckChance, Is.EqualTo(0.25));
                Assert.That(group.SkillChance, Is.EqualTo(1));
            }
        }
    }
}
