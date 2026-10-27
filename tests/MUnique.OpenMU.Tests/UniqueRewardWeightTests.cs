// <copyright file="UniqueRewardWeightTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Pins unique-set weighting independently of character equip eligibility.
/// </summary>
[TestFixture]
public class UniqueRewardWeightTests
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

    [TestCase(8, 6, .7)]
    [TestCase(9, 7, .7)]
    [TestCase(10, 7, .7)]
    [TestCase(11, 7, .7)]
    [TestCase(12, 9, .7)]
    [TestCase(5, 6, .4125)]
    [TestCase(6, 6, .525)]
    [TestCase(2, 7, .32)]
    public void EachUniqueArmorSetHasOneEqualAllocation(byte level, int count, double total)
    {
        var armor = this.Groups(level).Where(group => group.PossibleItems.Count > 0 && group.PossibleItems.All(item => item.Group is >= 7 and <= 11)).ToArray();
        Assert.That(armor.Length, Is.EqualTo(count));
        Assert.That(armor.Sum(group => group.Chance), Is.EqualTo(total).Within(1e-12));
        foreach (var group in armor)
        {
            Assert.That(group.PossibleItems.Select(item => item.Number).Distinct().Count(), Is.EqualTo(1));
            Assert.That(group.Chance, Is.EqualTo(total / count).Within(1e-12));
        }

        Assert.That(armor.Select(group => group.PossibleItems.First().Number).Distinct().Count(), Is.EqualTo(count));
    }

    [TestCase(1)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(11)]
    [TestCase(12)]
    public void WeaponsAndShieldsHaveNoRepeatedItemRoutes(byte level)
    {
        foreach (var category in new[] { false, true })
        {
            var items = this.Groups(level).SelectMany(group => group.PossibleItems)
                .Where(item => category ? item.Group == 6 : item.Group < 6).ToArray();
            Assert.That(items.Select(item => (item.Group, item.Number)).Distinct().Count(), Is.EqualTo(items.Length));
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(11)]
    [TestCase(12)]
    public void FiveHundredThousandActualOpeningsMatchEachItemWeight(byte level)
    {
        const int samples = 500_000;
        var groups = this.Groups(level);
        var expected = groups.SelectMany(group => group.PossibleItems.Select(item =>
                (Key: (item.Group, item.Number), Chance: group.Chance / group.PossibleItems.Count)))
            .GroupBy(row => row.Key).ToDictionary(rows => rows.Key, rows => rows.Sum(row => row.Chance));
        var observed = expected.Keys.ToDictionary(key => key, _ => 0);
        var generator = new DefaultDropGenerator(this._configuration, new SeededRandomizer(270000 + level));
        var money = 0;
        var secondOptions = 0;
        for (var draw = 0; draw < samples; draw++)
        {
            var result = generator.GenerateItemDrop(groups);
            if (result.Money is not null)
            {
                money++;
                Assert.That(result.Item, Is.Null);
                continue;
            }

            Assert.That(result.Item, Is.Not.Null);
            var item = result.Item!;
            observed[(item.Definition!.Group, item.Definition.Number)]++;
            if (level >= 8)
            {
                Assert.That(item.Level, Is.Zero);
                Assert.That(item.SocketCount, Is.Zero);
                var options = item.ItemOptions.Where(option => option.ItemOption?.OptionType == ItemOptionTypes.Excellent).ToArray();
                Assert.That(options.Length, Is.InRange(1, 2));
                Assert.That(options.Select(option => option.ItemOption).Distinct().Count(), Is.EqualTo(options.Length));
                secondOptions += options.Length == 2 ? 1 : 0;
            }
        }

        foreach (var (key, chance) in expected)
        {
            var tolerance = 6 * Math.Sqrt(samples * chance * (1 - chance));
            Assert.That(observed[key], Is.EqualTo(samples * chance).Within(Math.Max(12, tolerance)), $"Level {level}, item {key}");
        }

        var moneyChance = groups.Where(group => group.ItemType == SpecialItemType.Money).Sum(group => group.Chance);
        Assert.That((double)money / samples, Is.EqualTo(moneyChance).Within(.003));
        if (level >= 8)
        {
            Assert.That((double)secondOptions / samples, Is.EqualTo((13 - level) / 10.0).Within(.003));
        }

        TestContext.WriteLine($"Source level {level}: {samples:N0} actual openings; all {expected.Count} item probabilities verified; Zen outcomes {money}.");
    }

    [Test]
    public async Task LegacyRoutesNormalizeWithoutChangingPoolsRulesOrUnrelatedSystemsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        new BonusProgression(context, configuration).Initialize();
        new KundunProgression(context, configuration).Initialize();
        new AbsoluteBonusProgression(context, configuration).Initialize();
        var wrapper = configuration.Items.Single(item => item.Group == 14 && item.Number == 11);
        var before = wrapper.DropItems.ToArray();
        var protectedBefore = ProtectionFingerprint(configuration);
        Assert.That(before.Where(group => group.SourceItemLevel == 8 && group.PossibleItems.Any(item => item.Group == 8 && item.Number == 5))
            .Sum(group => group.Chance), Is.EqualTo(.25).Within(1e-12), "Reproduces Leather's old four-route inflation.");
        new UniqueRewardWeights(context, configuration).Initialize();
        Assert.That(ProtectionFingerprint(configuration), Is.EqualTo(protectedBefore));
        foreach (var level in new byte[] { 1, 2, 3, 5, 6, 8, 9, 10, 11, 12 })
        {
            var old = before.Where(group => group.SourceItemLevel == level).ToArray();
            var current = wrapper.DropItems.Where(group => group.SourceItemLevel == level).ToArray();
            Assert.That(ItemRules(current), Is.EqualTo(ItemRules(old)), $"Pool and generation rules at level {level}");
            foreach (var category in new[] { 0, 1, 2 })
            {
                Assert.That(CategoryChance(current, category), Is.EqualTo(CategoryChance(old, category)).Within(1e-12));
            }

            VerifyAveragedRoutes(old, current, false);
            VerifyAveragedRoutes(old, current, true);
        }

        Assert.That(wrapper.DropItems.Where(group => group.SourceItemLevel == 1 && group.PossibleItems.Any(item => item.Group >= 7)),
            Is.EquivalentTo(before.Where(group => group.SourceItemLevel == 1 && group.PossibleItems.Any(item => item.Group >= 7))), "Star armor stays untouched.");
        var first = ItemRules(wrapper.DropItems) + string.Join(';', wrapper.DropItems.Select(group => $"{group.Description}:{group.Chance:R}"));
        new UniqueRewardWeights(context, configuration).Initialize();
        Assert.That(ItemRules(wrapper.DropItems) + string.Join(';', wrapper.DropItems.Select(group => $"{group.Description}:{group.Chance:R}")), Is.EqualTo(first));
    }

    private static void VerifyAveragedRoutes(ItemDropItemGroup[] before, ItemDropItemGroup[] after, bool shields)
    {
        bool Selected(ItemDropItemGroup group) => group.PossibleItems.Count > 0 && group.PossibleItems.All(item => shields ? item.Group == 6 : item.Group < 6);
        var groups = before.Where(Selected).ToArray();
        var routes = groups.SelectMany(group => group.PossibleItems.Select(item => (Key: (item.Group, item.Number), Weight: group.Chance / group.PossibleItems.Count)))
            .GroupBy(row => row.Key).ToArray();
        if (routes.All(rows => rows.Count() == 1))
        {
            Assert.That(after.Where(Selected), Is.EquivalentTo(groups), "No duplicate means no rewrite.");
            return;
        }

        var averages = routes.ToDictionary(rows => rows.Key, rows => rows.Average(row => row.Weight));
        var total = groups.Sum(group => group.Chance);
        foreach (var group in after.Where(Selected))
        {
            var item = group.PossibleItems.Single();
            Assert.That(group.Chance, Is.EqualTo(total * averages[(item.Group, item.Number)] / averages.Values.Sum()).Within(1e-12));
        }
    }

    private static double CategoryChance(IEnumerable<ItemDropItemGroup> groups, int category) => groups.Where(group => group.PossibleItems.Count > 0
        && group.PossibleItems.All(item => category == 0 ? item.Group is >= 7 and <= 11 : category == 1 ? item.Group < 6 : item.Group == 6)).Sum(group => group.Chance);

    private static string ItemRules(IEnumerable<ItemDropItemGroup> groups) => string.Join('\n', groups.SelectMany(group => group.PossibleItems
        .Select(item => $"{group.SourceItemLevel}:{item.Group}/{item.Number}:{group.ItemType}:{group.MinimumLevel}:{group.MaximumLevel}:{group.RequiredCharacterLevel}:{group.ExcellentSecondOptionChance}:{group.SkillChance}:{group.LuckChance}:{group.SuppressAdditionalOption}:{group.GuaranteeJewelryOption}:{group.DropEffect}"))
        .Distinct().Order());

    private static string ProtectionFingerprint(GameConfiguration configuration) => string.Join('\n', configuration.Items.Select(item =>
            $"{item.Group}/{item.Number}:{item.MaximumSockets}:{string.Join(',', item.QualifiedCharacters.Select(character => character.Number).Order())}:" +
            string.Join(';', item.DropItems.Where(group => !(item.Group == 14 && item.Number == 11 && (group.SourceItemLevel is 1 or 2 or 5 or 6 or >= 8 and <= 12) && group.PossibleItems.Count > 0 && group.PossibleItems.All(reward => reward.Group < 12)))
                .Select(group => $"{group.GetId()}:{group.Chance:R}:{ItemRules([group])}").Order())))
        + string.Join(';', configuration.Maps.SelectMany(map => map.DropItemGroups.Select(group => $"{map.Number}:{group.GetId()}:{group.Chance:R}:{group.ItemLevel}")))
        + string.Join(';', configuration.MiniGameDefinitions.SelectMany(game => game.Rewards.Select(reward => $"{game.GetId()}:{reward.GetId()}:{reward.RewardAmount}:{reward.ItemReward?.GetId()}")))
        + string.Join(';', configuration.Monsters.Where(monster => monster.Number == 450).SelectMany(monster => monster.ItemCraftings.Select(recipe => $"{recipe.GetId()}:{recipe.SimpleCraftingSettings?.ResultItems.Count}")));

    private ItemDropItemGroup[] Groups(byte level) => this._configuration.Items.Single(item => item.Group == 14 && item.Number == 11)
        .DropItems.Where(group => group.SourceItemLevel == level).ToArray();
}
