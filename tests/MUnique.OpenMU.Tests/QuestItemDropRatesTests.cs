// <copyright file="QuestItemDropRatesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Verifies the drop chances of the class quest items.
/// </summary>
[TestFixture]
public class QuestItemDropRatesTests
{
    /// <summary>
    /// A new database has the rarer quest items.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasRarerQuestItems()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        AssertRates(configuration);
    }

    /// <summary>
    /// The update fixes an existing database exactly once.
    /// </summary>
    [Test]
    public async Task StartupInstallerFixesAnExistingDatabaseOnce()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        foreach (var requirement in QuestItemRequirements(configuration))
        {
            requirement.DropItemGroup!.Chance = 0.5;
        }

        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.QuestItemDropRates)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await QuestItemDropRatesInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        AssertRates(configuration);
        Assert.That(await QuestItemDropRatesInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
    }

    private static IEnumerable<DataModel.Configuration.Quests.QuestItemRequirement> QuestItemRequirements(GameConfiguration configuration)
    {
        return configuration.Monsters.SelectMany(monster => monster.Quests)
            .Where(quest => quest.Group == QuestItemDropRates.LegacyQuestGroup)
            .Distinct()
            .SelectMany(quest => quest.RequiredItems.Select(requirement => (quest, requirement)))
            .Select(pair => pair.requirement);
    }

    private static void AssertRates(GameConfiguration configuration)
    {
        var quests = configuration.Monsters.SelectMany(monster => monster.Quests)
            .Where(quest => quest.Group == QuestItemDropRates.LegacyQuestGroup)
            .Distinct()
            .ToList();
        foreach (var quest in quests.Where(quest => quest.Number <= 3))
        {
            Assert.That(quest.RequiredItems, Is.Not.Empty, quest.Name);
            foreach (var requirement in quest.RequiredItems)
            {
                var expected = quest.Number <= 1 ? QuestItemDropRates.FirstClassChange : QuestItemDropRates.HeroAndDarkStone;
                Assert.That(requirement.DropItemGroup!.Chance, Is.EqualTo(expected).Within(1e-12), quest.Name);
            }
        }

        var evidence = quests.Single(quest => quest.Number == 4);
        Assert.That(
            evidence.RequiredItems.Select(requirement => requirement.DropItemGroup!.Chance).Order(),
            Is.EqualTo(new[] { 0.015, 0.02, 0.025 }).Within(1e-12));
    }
}
