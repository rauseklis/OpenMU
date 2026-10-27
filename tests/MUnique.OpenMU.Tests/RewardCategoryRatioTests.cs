// <copyright file="RewardCategoryRatioTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Pins the approved equipment category totals without changing reward contents.
/// </summary>
[TestFixture]
public class RewardCategoryRatioTests
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

    [TestCase(11, 8, .70, .20, .10, 0, 0, 0, 0)]
    [TestCase(11, 9, .70, .20, .10, 0, 0, 0, 0)]
    [TestCase(11, 10, .70, .20, .10, 0, 0, 0, 0)]
    [TestCase(11, 11, .70, .20, .10, 0, 0, 0, 0)]
    [TestCase(11, 12, .70, .20, .10, 0, 0, 0, 0)]
    [TestCase(11, 5, .4125, .11, .0275, 0, .20, .05, .20)]
    [TestCase(11, 6, .525, .14, .035, 0, .15, .05, .10)]
    [TestCase(11, 2, .32, .08, 0, 0, .35, 0, .25)]
    [TestCase(11, 1, .75, .20, .05, 0, 0, 0, 0)]
    [TestCase(32, 0, .70, .20, .10, 0, 0, 0, 0)]
    [TestCase(34, 0, .80, .15, 0, .05, 0, 0, 0)]
    [TestCase(11, 3, 0, .525, .075, 0, .30, 0, .10)]
    public void ConfiguredCategoriesMatchApprovedTotals(short number, byte level, double armor, double weapons, double shields, double rare, double jewels, double rings, double zen)
    {
        var groups = this.Groups(number, level);
        Assert.That(groups.Sum(group => group.Chance), Is.EqualTo(1).Within(1e-12));
        Assert.That(Chance(groups, "armor"), Is.EqualTo(armor).Within(1e-12));
        Assert.That(Chance(groups, "weapons"), Is.EqualTo(weapons).Within(1e-12));
        Assert.That(Chance(groups, "shields"), Is.EqualTo(shields).Within(1e-12));
        Assert.That(Chance(groups, "rare"), Is.EqualTo(rare).Within(1e-12));
        Assert.That(groups.Where(group => group.Description.Value!.EndsWith(" / jewels", StringComparison.Ordinal)).Sum(group => group.Chance), Is.EqualTo(jewels).Within(1e-12));
        Assert.That(groups.Where(group => group.Description.Value!.EndsWith(" / rings", StringComparison.Ordinal)).Sum(group => group.Chance), Is.EqualTo(rings).Within(1e-12));
        Assert.That(groups.Where(group => group.ItemType == SpecialItemType.Money).Sum(group => group.Chance), Is.EqualTo(zen).Within(1e-12));
    }

    [TestCase(11, 8)]
    [TestCase(11, 9)]
    [TestCase(11, 10)]
    [TestCase(11, 11)]
    [TestCase(11, 12)]
    [TestCase(11, 5)]
    [TestCase(11, 6)]
    [TestCase(11, 2)]
    [TestCase(11, 1)]
    [TestCase(32, 0)]
    [TestCase(34, 0)]
    public void FiveHundredThousandActualOpeningsKeepEachNormalizedItemWeight(short number, byte level)
    {
        const int samples = 500_000;
        var groups = this.Groups(number, level);
        var expected = groups.SelectMany(group => group.PossibleItems.Select(item =>
                (Key: (item.Group, item.Number), Chance: group.Chance / group.PossibleItems.Count)))
            .GroupBy(row => row.Key).ToDictionary(rows => rows.Key, rows => rows.Sum(row => row.Chance));
        var observed = expected.Keys.ToDictionary(key => key, _ => 0);
        var generator = new DefaultDropGenerator(this._configuration, new SeededRandomizer(155000 + number + level));
        var money = 0;
        for (var draw = 0; draw < samples; draw++)
        {
            var result = generator.GenerateItemDrop(groups);
            if (result.Money is not null)
            {
                money++;
                Assert.That(result.Item, Is.Null);
            }
            else
            {
                Assert.That(result.Item, Is.Not.Null);
                var item = result.Item!;
                observed[(item.Definition!.Group, item.Definition.Number)]++;
            }
        }

        foreach (var (key, chance) in expected)
        {
            var tolerance = Math.Max(12, 6 * Math.Sqrt(samples * chance * (1 - chance)));
            Assert.That(observed[key], Is.EqualTo(samples * chance).Within(tolerance), $"Box {number}/{level}, item {key}");
        }

        Assert.That((double)money / samples, Is.EqualTo(groups.Where(group => group.ItemType == SpecialItemType.Money).Sum(group => group.Chance)).Within(.003));
        TestContext.WriteLine($"Box {number}/{level}: {samples:N0} actual openings; all {expected.Count} normalized item probabilities verified.");
    }

    [Test]
    public async Task OnlyCategoryWeightsChangeAndInternalProportionsStayExactAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        new BonusProgression(context, configuration).Initialize();
        new KundunProgression(context, configuration).Initialize();
        new ChocolateEndgameRewards(context, configuration).Initialize();
        new AbsoluteBonusProgression(context, configuration).Initialize();
        new UniqueRewardWeights(context, configuration).Initialize();
        var rules = ProtectedRules(configuration);
        var before = configuration.Items.SelectMany(item => item.DropItems.Where(group => MayChange(item.Group, item.Number, group))
            .Select(group => (Group: group, OldChance: group.Chance))).ToArray();
        new RewardCategoryRatios(context, configuration).Initialize();
        Assert.That(ProtectedRules(configuration), Is.EqualTo(rules), "Only specified equipment category chances may change.");
        foreach (var category in before.GroupBy(row => (row.Group.SourceItemLevel, Category: row.Group.PossibleItems.First().Group >= 7 ? "armor" : row.Group.PossibleItems.First().Group == 6 ? "shield" : "weapon", Box: configuration.Items.Single(item => item.DropItems.Contains(row.Group)).Number)))
        {
            var oldTotal = category.Sum(row => row.OldChance);
            var newTotal = category.Sum(row => row.Group.Chance);
            foreach (var row in category)
            {
                Assert.That(row.Group.Chance / newTotal, Is.EqualTo(row.OldChance / oldTotal).Within(1e-12), "Internal variant proportions must stay unchanged.");
            }
        }

        var chances = string.Join(';', configuration.Items.SelectMany(item => item.DropItems.Select(group => $"{group.GetId()}:{group.Chance:R}")));
        new RewardCategoryRatios(context, configuration).Initialize();
        Assert.That(string.Join(';', configuration.Items.SelectMany(item => item.DropItems.Select(group => $"{group.GetId()}:{group.Chance:R}"))), Is.EqualTo(chances), "Reapplication must be idempotent.");
    }

    private static bool MayChange(byte boxGroup, short box, ItemDropItemGroup group) => boxGroup == 14 && ((box == 11 && group.SourceItemLevel is 1 or 2 or 5 or 6 or >= 8 and <= 12)
        || (box is 32 or 34 && group.SourceItemLevel == 0)) && group.PossibleItems.Count > 0
        && group.PossibleItems.All(item => item.Group < 12 && !(item.Group == 2 && item.Number == 22));

    private static string ProtectedRules(GameConfiguration configuration) => string.Join('\n', configuration.Items.OrderBy(item => item.Group).ThenBy(item => item.Number).Select(item =>
            $"{item.GetId()}:{item.Group}/{item.Number}:{item.MaximumSockets}:{item.Skill?.GetId()}:{string.Join(',', item.QualifiedCharacters.Select(character => character.Number).Order())}:" +
            string.Join(';', item.Requirements.Select(requirement => $"{requirement.Attribute?.GetId()}:{requirement.MinimumValue}")) +
            string.Join(';', item.DropItems.Select(group => $"{group.GetId()}:{group.SourceItemLevel}:{(MayChange(item.Group, item.Number, group) ? "scaled" : group.Chance.ToString("R"))}:{group.ItemType}:{group.MinimumLevel}:{group.MaximumLevel}:{group.RequiredCharacterLevel}:{group.ExcellentSecondOptionChance}:{group.SkillChance}:{group.LuckChance}:{group.SuppressAdditionalOption}:{group.GuaranteeJewelryOption}:{group.DropEffect}:{group.MoneyAmount}:{string.Join(',', group.PossibleItems.Select(reward => $"{reward.Group}/{reward.Number}"))}"))))
        + string.Join(';', configuration.Maps.SelectMany(map => map.DropItemGroups.Select(group => $"{map.Number}:{group.GetId()}:{group.Chance:R}:{group.ItemLevel}")))
        + string.Join(';', configuration.MiniGameDefinitions.SelectMany(game => game.Rewards.Select(reward => $"{game.GetId()}:{reward.GetId()}:{reward.RewardAmount}:{reward.ItemReward?.GetId()}")))
        + string.Join(';', configuration.Monsters.SelectMany(monster => monster.ItemCraftings.Select(recipe => $"{recipe.GetId()}:{recipe.SimpleCraftingSettings?.ResultItems.Count}")));

    private static double Chance(IEnumerable<ItemDropItemGroup> groups, string category) => groups.Where(group => group.PossibleItems.Count > 0
        && group.PossibleItems.All(item => category switch
        {
            "armor" => item.Group is >= 7 and <= 11,
            "weapons" => item.Group < 6 && !(item.Group == 2 && item.Number == 22),
            "shields" => item.Group == 6,
            _ => item.Group == 2 && item.Number == 22,
        })).Sum(group => group.Chance);

    private ItemDropItemGroup[] Groups(short number, byte level) => this._configuration.Items.Single(item => item.Group == 14 && item.Number == number)
        .DropItems.Where(group => group.SourceItemLevel == level).ToArray();
}
