// <copyright file="BonusProgressionTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Verifies the approved ordinary bonus progression without changing event rewards.
/// </summary>
[TestFixture]
public class BonusProgressionTests
{
    private GameConfiguration _configuration = null!;

    [OneTimeSetUp]
    public async Task InitializeAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        this._configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
    }

    [Test]
    public void StarHasOnlyApprovedLevelsAndFourPlainShields()
    {
        var groups = this.Groups(1);
        Assert.That(groups.Sum(group => group.Chance), Is.EqualTo(1).Within(1e-12));
        var shields = groups.Where(group => group.PossibleItems.All(item => item.Group == 6)).ToList();
        Assert.That(shields.SelectMany(group => group.PossibleItems).Select(item => item.Number),
            Is.EquivalentTo(new short[] { 13, 13, 15, 11, 16 }));
        foreach (var group in shields)
        {
            var expectedLevel = group.PossibleItems.First().Number is 11 or 16 ? 8 : 7;
            Assert.That(group.MinimumLevel, Is.EqualTo(expectedLevel));
            Assert.That(group.MaximumLevel, Is.EqualTo(expectedLevel));
            Assert.That(group.LuckChance, Is.Zero);
            Assert.That(group.SkillChance, Is.Zero);
        }

        foreach (var group in groups.Except(shields))
        {
            Assert.That(group.MinimumLevel, Is.EqualTo(6));
            Assert.That(group.MaximumLevel, Is.EqualTo(9));
            Assert.That(group.LuckChance, Is.Null);
        }
    }

    [Test]
    public void KundunUsesSixtyThirtyTenAndPreservesExcellentOdds()
    {
        for (byte level = 8; level <= 12; level++)
        {
            var groups = this.Groups(level);
            Assert.That(groups.Where(group => group.PossibleItems.All(item => item.Group is >= 7 and <= 11)).Sum(group => group.Chance), Is.EqualTo(.60).Within(1e-12));
            Assert.That(groups.Where(group => group.PossibleItems.All(item => item.Group < 6)).Sum(group => group.Chance), Is.EqualTo(.30).Within(1e-12));
            Assert.That(groups.Where(group => group.PossibleItems.All(item => item.Group == 6)).Sum(group => group.Chance), Is.EqualTo(.10).Within(1e-12));
            Assert.That(groups.All(group => group.ItemType == SpecialItemType.Excellent && group.MinimumLevel == 0 && group.MaximumLevel == 0), Is.True);
            Assert.That(groups.All(group => Math.Abs(group.ExcellentSecondOptionChance!.Value - new[] { .30, .25, .20, .15, .10 }[level - 8]) < 1e-12), Is.True);
        }
    }

    [Test]
    public void HeartLevelsAndBooksMatchApprovedPlan()
    {
        Assert.That(this.Groups(3).Where(group => group.PossibleItems.Any(item => item.Group <= 6))
            .All(group => group.MinimumLevel == 8 && group.MaximumLevel == 10), Is.True);
        Assert.That(this.Groups(2).SelectMany(group => group.PossibleItems).Any(item => item.Group == 5 && item.Number == 22), Is.True);
        foreach (var level in new byte[] { 1, 2, 3, 5, 6 })
        {
            Assert.That(this.Groups(level).Where(group => group.PossibleItems.Any(item => item.Group == 5 && item.Number is >= 21 and <= 23))
                .All(group => group.SkillChance == 1), Is.True);
        }
    }

    [Test]
    public void StarBalancesClassesAndCategoriesWithoutFirecrackerOverlap()
    {
        var groups = this.Groups(1);
        foreach (var name in new[] { "DW", "DK", "Elf", "MG", "DL", "Summoner", "RF" })
        {
            Assert.That(groups.Where(group => group.Description.Value!.StartsWith($"Approved Star: {name} /", StringComparison.Ordinal))
                .Sum(group => group.Chance), Is.EqualTo(1.0 / 7).Within(1e-12), name);
        }

        Assert.That(groups.Where(group => group.PossibleItems.All(item => item.Group is >= 7 and <= 11)).Sum(group => group.Chance), Is.EqualTo(.60).Within(1e-12));
        Assert.That(groups.Where(group => group.PossibleItems.All(item => item.Group < 6)).Sum(group => group.Chance), Is.EqualTo(.35).Within(1e-12));
        Assert.That(groups.Where(group => group.PossibleItems.All(item => item.Group == 6)).Sum(group => group.Chance), Is.EqualTo(.05).Within(1e-12));
        var firecracker = this.Groups(2).SelectMany(group => group.PossibleItems).ToHashSet();
        Assert.That(groups.SelectMany(group => group.PossibleItems).Any(firecracker.Contains), Is.False);
        Assert.That(groups.SelectMany(group => group.PossibleItems).Any(item => item.Group == 2 && item.Number == 7), Is.True, "EE mace");
        Assert.That(groups.SelectMany(group => group.PossibleItems).Any(item => item.Group == 5 && item.Number == 23), Is.True, "Lagle");
    }

    [Test]
    public void MonsterProgressionUsesExactMapsAndLeavesExistingHeavenSources()
    {
        var pairs = this._configuration.Maps.SelectMany(map => map.DropItemGroups
            .Where(group => group.ItemType == SpecialItemType.SmallBonus)
            .Select(group => (Map: (int)map.Number, Monster: (int)group.Monster!.Number, Level: (int)group.ItemLevel!.Value))).ToList();
        Assert.That(pairs.Count, Is.EqualTo(69));
        Assert.That(pairs.Distinct().Count(), Is.EqualTo(pairs.Count));
        Assert.That(pairs.Where(pair => pair.Level == 1).Select(pair => (pair.Map, pair.Monster)), Is.EquivalentTo(new[]
        {
            (8, 62), (8, 60), (8, 57), (8, 58), (8, 61),
            (10, 69), (10, 71), (10, 70), (10, 73),
            (33, 308), (33, 307), (33, 306), (33, 305),
        }));
        Assert.That(pairs.Count(pair => pair.Level == 7), Is.EqualTo(30), "Existing Heaven sources");
        Assert.That(pairs.Count(pair => pair.Level == 2), Is.EqualTo(8));
        Assert.That(pairs.Where(pair => pair.Level == 2).All(pair => pair.Map is 4 or 7), Is.True);
    }

    private List<ItemDropItemGroup> Groups(byte level) => this._configuration.Items.Single(item => item.Group == 14 && item.Number == 11)
        .DropItems.Where(group => group.SourceItemLevel == level).ToList();
}
