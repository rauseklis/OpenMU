// <copyright file="TierTwoAncientStatsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Checks complete, incomplete and broken mid-tier Ancient sets against real game rules.
/// </summary>
[TestFixture]
public class TierTwoAncientStatsTests
{
    /// <summary>
    /// Existing bonuses survive; only a complete, unbroken set grants the four new stats.
    /// </summary>
    /// <param name="name">The Ancient set name.</param>
    /// <param name="bonus">The additional amount of each stat.</param>
    /// <param name="originalCount">The original option count.</param>
    [TestCase("Garuda", 50, 6)]
    [TestCase("Heras", 50, 9)]
    [TestCase("Argo", 50, 4)]
    [TestCase("Eplete", 50, 7)]
    [TestCase("Semeden", 50, 6)]
    [TestCase("Chamer", 50, 7)]
    public async Task FullSetStatsPreserveExistingOptionsAsync(string name, int bonus, int originalCount)
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var set = configuration.ItemSetGroups.Single(group => group.Name == name);
        var originalOptions = set.Options!.PossibleOptions.OrderBy(option => option.Number).Take(originalCount).ToArray();
        var unrelated = configuration.ItemSetGroups.Single(group => group.Name == "Hyon").Options!.PossibleOptions.ToArray();
        var initializer = new TierTwoAncientStats(context, configuration);
        initializer.Initialize();
        initializer.Initialize();
        Assert.That(set.Options.PossibleOptions.Count, Is.EqualTo(originalCount + 4));
        Assert.That(set.Options.PossibleOptions.OrderBy(option => option.Number).Take(originalCount), Is.EqualTo(originalOptions));
        Assert.That(configuration.ItemSetGroups.Single(group => group.Name == "Hyon").Options!.PossibleOptions, Is.EqualTo(unrelated));

        var addedOptions = set.Options.PossibleOptions.OrderBy(option => option.Number).Skip(originalCount).ToArray();
        var attributes = new[] { Stats.TotalStrength, Stats.TotalAgility, Stats.TotalVitality, Stats.TotalEnergy };
        Assert.That(addedOptions.Select(option => option.PowerUpDefinition!.TargetAttribute), Is.EquivalentTo(attributes));
        Assert.That(addedOptions.All(option => option.PowerUpDefinition!.Boost!.ConstantValue.Value == bonus), Is.True);
        Assert.That(addedOptions.All(option => option.PowerUpDefinition!.Boost!.ConstantValue.AggregateType == AggregateType.AddRaw), Is.True);

        var items = set.Items.Select(member =>
        {
            var item = context.CreateNew<Item>();
            item.Definition = member.ItemDefinition;
            item.Durability = 1;
            item.ItemSetGroups.Add(member);
            return item;
        }).ToArray();
        var holder = new AttributeSystem([], [], []);
        var factory = new ItemPowerUpFactory(NullLogger<ItemPowerUpFactory>.Instance);
        var full = factory.GetSetPowerUps(items, holder, configuration).ToArray();
        foreach (var attribute in attributes)
        {
            var originalValue = originalOptions.Where(option => option.PowerUpDefinition!.TargetAttribute == attribute)
                .Sum(option => option.PowerUpDefinition!.Boost!.ConstantValue.Value);
            Assert.That(holder[attribute], Is.EqualTo(originalValue + bonus));
        }

        Assert.That(holder[Stats.TotalLeadership], Is.Zero);

        foreach (var wrapper in full)
        {
            wrapper.Dispose();
        }

        for (var missing = 0; missing < items.Length; missing++)
        {
            items[missing].Durability = 0;
            var partial = factory.GetSetPowerUps(items, holder, configuration).ToArray();
            // Other equipment effects (such as the normal armor-set bonus) may remain active.
            foreach (var attribute in attributes)
            {
                var originalValue = originalOptions.Take(items.Length - 2)
                    .Where(option => option.PowerUpDefinition!.TargetAttribute == attribute)
                    .Sum(option => option.PowerUpDefinition!.Boost!.ConstantValue.Value);
                Assert.That(holder[attribute], Is.EqualTo(originalValue));
            }
            Assert.That(holder[Stats.TotalLeadership], Is.Zero);
            foreach (var wrapper in partial)
            {
                wrapper.Dispose();
            }

            items[missing].Durability = 1;
        }
    }
}
