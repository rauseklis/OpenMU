// <copyright file="MoveBrassToGoldMedalTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Verifies that Brass armor is a Gold Medal reward and Bronze armor a Silver Medal reward.
/// </summary>
[TestFixture]
public class MoveBrassToGoldMedalTests
{
    private const short Bronze = 0;
    private const short Brass = 8;

    /// <summary>
    /// A new Season 6 database has Bronze in the Silver and Brass in the Gold Medal rewards.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasBronzeInSilverAndBrassInGold()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        AssertNewState(configuration);
    }

    /// <summary>
    /// An existing database is changed exactly once.
    /// </summary>
    [Test]
    public async Task StartupInstallerUpdatesAnExistingDatabaseOnce()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var wrapper = configuration.Items.Single(item => item.Group == 14 && item.Number == 11);

        // Rebuild the previous state: Brass in Silver, Bronze not rewarded, six Gold sets.
        var silverBronze = Armor(wrapper, 5).Single(group => group.PossibleItems.Any(item => item.Number == Bronze));
        silverBronze.PossibleItems.Clear();
        foreach (var item in configuration.Items.Where(item => item.Group is >= 7 and <= 11 && item.Number == Brass))
        {
            silverBronze.PossibleItems.Add(item);
        }

        var goldBrass = Armor(wrapper, 6).Single(group => group.PossibleItems.Any(item => item.Number == Brass));
        wrapper.DropItems.Remove(goldBrass);
        var gold = Armor(wrapper, 6).ToArray();
        foreach (var group in gold)
        {
            group.Chance = .525 / gold.Length;
        }

        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.MoveBrassToGoldMedal)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await MoveBrassToGoldMedalInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        AssertNewState(configuration);
        Assert.That(await MoveBrassToGoldMedalInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
        AssertNewState(configuration);
    }

    private static IEnumerable<ItemDropItemGroup> Armor(ItemDefinition wrapper, byte level)
        => wrapper.DropItems.Where(group => group.SourceItemLevel == level && group.PossibleItems.Count > 0
            && group.PossibleItems.All(item => item.Group is >= 7 and <= 11));

    private static void AssertNewState(GameConfiguration configuration)
    {
        var wrapper = configuration.Items.Single(item => item.Group == 14 && item.Number == 11);
        var silver = Armor(wrapper, 5).ToArray();
        var gold = Armor(wrapper, 6).ToArray();

        Assert.That(silver.Length, Is.EqualTo(6));
        Assert.That(silver.Sum(group => group.Chance), Is.EqualTo(.4125).Within(1e-12));
        Assert.That(silver.SelectMany(group => group.PossibleItems).Select(item => item.Number), Does.Contain(Bronze));
        Assert.That(silver.SelectMany(group => group.PossibleItems).Select(item => item.Number), Does.Not.Contain(Brass));

        Assert.That(gold.Length, Is.EqualTo(7));
        Assert.That(gold.Sum(group => group.Chance), Is.EqualTo(.525).Within(1e-12));
        Assert.That(gold.SelectMany(group => group.PossibleItems).Select(item => item.Number), Does.Contain(Brass));
        foreach (var group in gold)
        {
            Assert.That(group.Chance, Is.EqualTo(.525 / 7).Within(1e-12));
            Assert.That(group.PossibleItems.Select(item => item.Number).Distinct().Count(), Is.EqualTo(1));
        }

        var brassGroup = gold.Single(group => group.PossibleItems.First().Number == Brass);
        Assert.That(brassGroup.PossibleItems.Count, Is.EqualTo(5));
        var bronzeGroup = silver.Single(group => group.PossibleItems.First().Number == Bronze);
        Assert.That(bronzeGroup.PossibleItems.Count, Is.EqualTo(5));
    }
}
