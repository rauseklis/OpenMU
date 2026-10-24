// <copyright file="TierOneAncientStatsTests.cs" company="MUnique">
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
/// Checks complete, incomplete and broken low-tier Ancient sets against real game rules.
/// </summary>
[TestFixture]
public class TierOneAncientStatsTests
{
    /// <summary>
    /// Existing bonuses survive; only a complete, unbroken set grants the four new stats.
    /// </summary>
    /// <param name="name">The Ancient set name.</param>
    /// <param name="bonus">The additional amount of each stat.</param>
    /// <param name="originalCount">The original option count.</param>
    [TestCase("Hyperion", 100, 4)]
    [TestCase("Barnake", 100, 4)]
    [TestCase("Fase", 100, 3)]
    [TestCase("Cloud", 75, 2)]
    public async Task FullSetStatsPreserveExistingOptionsAsync(string name, int bonus, int originalCount)
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var set = configuration.ItemSetGroups.Single(group => group.Name == name);
        var originalOptions = set.Options!.PossibleOptions.OrderBy(option => option.Number).Take(originalCount).ToArray();
        var unrelated = configuration.ItemSetGroups.Single(group => group.Name == "Hyon").Options!.PossibleOptions.ToArray();
        var initializer = new TierOneAncientStats(context, configuration);
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

        foreach (var wrapper in full)
        {
            wrapper.Dispose();
        }

        for (var missing = 0; missing < items.Length; missing++)
        {
            items[missing].Durability = 0;
            var partial = factory.GetSetPowerUps(items, holder, configuration).ToArray();
            Assert.That(partial.Length, Is.EqualTo(items.Length - 2));
            Assert.That(holder[Stats.TotalStrength], Is.Zero);
            Assert.That(holder[Stats.TotalVitality], Is.Zero);
            foreach (var wrapper in partial)
            {
                wrapper.Dispose();
            }

            items[missing].Durability = 1;
        }
    }
}
