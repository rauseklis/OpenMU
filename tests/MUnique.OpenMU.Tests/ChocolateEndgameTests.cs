// <copyright file="ChocolateEndgameTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Pins ordinary Chocolate rewards, class probabilities and global socket restrictions.
/// </summary>
[TestFixture]
public class ChocolateEndgameTests
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
    public void PinkCategoriesAndClassesHaveExactWeights()
    {
        var groups = this.Groups(32);
        Assert.That(this.Category(groups, "Armor"), Is.EqualTo(.70).Within(1e-12));
        Assert.That(this.Category(groups, "Weapon"), Is.EqualTo(.20).Within(1e-12));
        Assert.That(this.Category(groups, "Shield"), Is.EqualTo(.10).Within(1e-12));
        foreach (var name in new[] { "DW", "DK", "Elf", "MG", "DL", "SUM", "RF" })
        {
            var armor = groups.Where(group => group.Description.Value!.Contains($"/ Armor / {name} /", StringComparison.Ordinal)).Sum(group => group.Chance);
            Assert.That(armor, Is.EqualTo((name is "SUM" or "RF" ? .10 : .08) * .70 / .60).Within(1e-12), name);
            Assert.That(groups.Where(group => group.Description.Value!.Contains($"/ Weapon / {name} /", StringComparison.Ordinal)).Sum(group => group.Chance), Is.EqualTo(.20 / 7).Within(1e-12), name);
        }

        Assert.That(groups.All(group => group.MinimumLevel == 6 && group.MaximumLevel == 9), Is.True);
        Assert.That(groups.All(group => group.ItemType == SpecialItemType.RandomItem && group.SkillChance is null && group.LuckChance is null), Is.True);
        Assert.That(groups.Where(group => group.Description.Value!.Contains("/ Armor / DK /", StringComparison.Ordinal)).Select(group => group.Chance), Is.EqualTo(new[] { .04 * .70 / .60, .04 * .70 / .60 }).Within(1e-12));
        Assert.That(groups.Where(group => group.PossibleItems.All(item => item.Group == 6)).Select(group => group.Chance), Is.EqualTo(Enumerable.Repeat(.02, 5)));
    }

    [Test]
    public void BlueHasSingleGlobalRareMaceAndSixNormalClasses()
    {
        var groups = this.Groups(34);
        Assert.That(this.Category(groups, "Armor"), Is.EqualTo(.80).Within(1e-12));
        Assert.That(this.Category(groups, "Weapon"), Is.EqualTo(.15).Within(1e-12));
        var rare = groups.Single(group => group.Description.Value!.Contains("/ Rare /", StringComparison.Ordinal));
        Assert.That(rare.Chance, Is.EqualTo(.05));
        Assert.That(rare.PossibleItems.Select(item => ((int)item.Group, (int)item.Number)), Is.EqualTo(new[] { (2, 22) }));
        Assert.That(rare.MinimumLevel, Is.Zero);
        Assert.That(rare.MaximumLevel, Is.Zero);
        foreach (var name in new[] { "DW", "DK", "Elf", "MG", "DL", "SUM" })
        {
            Assert.That(groups.Where(group => group.Description.Value!.Contains($"/ Armor / {name} /", StringComparison.Ordinal)).Sum(group => group.Chance), Is.EqualTo(.80 / 6).Within(1e-12));
            Assert.That(groups.Where(group => group.Description.Value!.Contains($"/ Weapon / {name} /", StringComparison.Ordinal)).Sum(group => group.Chance), Is.EqualTo(.15 / 6).Within(1e-12));
        }

        Assert.That(groups.SelectMany(group => group.PossibleItems).Any(item => item.Group == 5 && item.Number == 20), Is.True);
        Assert.That(groups.All(group => !group.Description.Value!.Contains("/ RF /", StringComparison.Ordinal)), Is.True);
        Assert.That(groups.Where(group => group != rare).All(group => group.MinimumLevel == 6 && group.MaximumLevel == 9), Is.True);
    }

    [Test]
    public void RedIsGuaranteedRoyalAtZeroAndMacesHaveLogicalMaximumDamage()
    {
        var royal = this.Groups(33).Single();
        Assert.That(royal.Chance, Is.EqualTo(1));
        Assert.That(royal.MinimumLevel, Is.Zero);
        Assert.That(royal.MaximumLevel, Is.Zero);
        Assert.That(royal.PossibleItems.Select(item => ((int)item.Group, (int)item.Number)), Is.EqualTo(new[] { (2, 23) }));
        foreach (var (number, minimum, maximum) in new[] { (22, 150, 165), (23, 200, 220) })
        {
            var item = this._configuration.Items.Single(item => item.Group == 2 && item.Number == number);
            Assert.That(item.BasePowerUpAttributes.Single(power => power.TargetAttribute == Stats.MinimumPhysBaseDmgByWeapon).BaseValue, Is.EqualTo(minimum));
            Assert.That(item.BasePowerUpAttributes.Single(power => power.TargetAttribute == Stats.MaximumPhysBaseDmgByWeapon).BaseValue, Is.EqualTo(maximum));
            Assert.That(item.Skill, Is.Null);
        }
    }

    [Test]
    public void AllItemsHaveSocketsDisabledAndSeedNpcsCannotCraft()
    {
        Assert.That(this._configuration.Items.All(item => item.MaximumSockets == 0), Is.True);
        Assert.That(this._configuration.Monsters.Where(monster => monster.NpcWindow is NpcWindow.SeedMaster or NpcWindow.SeedResearcher).All(monster => monster.ItemCraftings.Count == 0), Is.True);
        foreach (var item in this.Groups(32).Concat(this.Groups(34)).SelectMany(group => group.PossibleItems))
        {
            Assert.That(item.Requirements.Any(requirement => requirement.Attribute == Stats.Level && requirement.MinimumValue == 380), Is.False, item.Name);
        }
    }

    [TestCase(32)]
    [TestCase(34)]
    public void OneHundredThousandActualOpeningsMatchContract(int number)
    {
        const int openings = 100_000;
        var groups = this.Groups(number);
        var generator = new DefaultDropGenerator(this._configuration, new SeededRandomizer(20261026 + number));
        var counts = new Dictionary<string, int>();
        var owners = groups.SelectMany(group => group.PossibleItems.Select(item =>
            (Key: ((int)item.Group, (int)item.Number), Category: group.Description.Value!.Split(" / ")[1], Class: group.Description.Value!.Split(" / ")[2])))
            .ToDictionary(entry => entry.Key, entry => (entry.Category, entry.Class));
        for (var draw = 0; draw < openings; draw++)
        {
            var result = generator.GenerateItemDrop(groups);
            Assert.That(result.Money, Is.Null);
            Assert.That(result.Item, Is.Not.Null);
            var item = result.Item!;
            Assert.That(item.IsExcellent() || item.IsAncient(), Is.False);
            Assert.That(item.SocketCount, Is.Zero);
            var owner = owners[((int)item.Definition!.Group, (int)item.Definition.Number)];
            counts[owner.Category] = counts.GetValueOrDefault(owner.Category) + 1;
            counts[$"{owner.Category}/{owner.Class}"] = counts.GetValueOrDefault($"{owner.Category}/{owner.Class}") + 1;
            Assert.That(item.Level, owner.Category == "Rare" ? Is.Zero : Is.InRange(6, 9));
        }

        foreach (var category in number == 32 ? new[] { ("Armor", .70), ("Weapon", .20), ("Shield", .10) } : new[] { ("Armor", .80), ("Weapon", .15), ("Rare", .05) })
        {
            var observed = (double)counts[category.Item1] / openings;
            TestContext.WriteLine($"Box {number}: {category.Item1} observed {observed:P4}, expected {category.Item2:P4}");
            Assert.That(observed, Is.EqualTo(category.Item2).Within(.005));
        }

        foreach (var group in groups.GroupBy(group => string.Join('/', group.Description.Value!.Split(" / ").Skip(1).Take(2))))
        {
            var observed = (double)counts[group.Key] / openings;
            Assert.That(observed, Is.EqualTo(group.Sum(entry => entry.Chance)).Within(.004), group.Key);
        }
    }

    [Test]
    public void SummonerProgressionAndStickAreConsistentAtEveryUpgrade()
    {
        var defense = 0f;
        foreach (var group in new[] { 7, 8, 9, 10, 11 })
        {
            var pink = this._configuration.Items.Single(item => item.Group == group && item.Number == 43);
            var blue = this._configuration.Items.Single(item => item.Group == group && item.Number == 44);
            defense += blue.BasePowerUpAttributes.Single(power => power.TargetAttribute == Stats.DefenseBase).BaseValue;
            for (byte level = 0; level <= 15; level++)
            {
                foreach (var attribute in new[] { Stats.TotalStrengthRequirementValue, Stats.TotalAgilityRequirementValue })
                {
                    var before = new TemporaryItem { Definition = pink, Level = level };
                    var after = new TemporaryItem { Definition = blue, Level = level };
                    Assert.That(after.GetRequirement(blue.Requirements.Single(requirement => requirement.Attribute == attribute)).Value,
                        Is.GreaterThan(before.GetRequirement(pink.Requirements.Single(requirement => requirement.Attribute == attribute)).Value), $"{blue.Name} +{level}");
                }
            }
        }

        Assert.That(defense, Is.EqualTo(268));
        var stick = this._configuration.Items.Single(item => item.Group == 5 && item.Number == 20);
        Assert.That(stick.BasePowerUpAttributes.Single(power => power.TargetAttribute == Stats.StaffRise).BaseValue, Is.EqualTo(65));
        Assert.That(stick.BasePowerUpAttributes.Single(power => power.TargetAttribute == Stats.MinimumPhysBaseDmgByWeapon).BaseValue, Is.EqualTo(66));
        Assert.That(stick.BasePowerUpAttributes.Single(power => power.TargetAttribute == Stats.MaximumPhysBaseDmgByWeapon).BaseValue, Is.EqualTo(74));
        Assert.That(stick.Requirements.Any(requirement => requirement.Attribute == Stats.Level), Is.False);
    }

    [Test]
    public async Task LegacySocketLinksCannotAddPowerUpsOrReduceRequirementsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var definition = this._configuration.Items.Single(item => item.Group == 2 && item.Number == 22);
        var item = new TemporaryItem { Definition = definition, Durability = definition.Durability, ItemSlot = 0, SocketCount = 3 };
        var factory = new ItemPowerUpFactory(NullLogger<ItemPowerUpFactory>.Instance);
        var baseline = factory.GetPowerUps(item, player.Attributes!).Sum(power => power.Value);
        var socket = this._configuration.ItemOptions.SelectMany(option => option.PossibleOptions)
            .First(option => option.OptionType == ItemOptionTypes.SocketOption && option.SubOptionType == (int)SocketSubOptionType.Fire && option.Number == 1);
        item.ItemOptions.Add(new MUnique.OpenMU.Persistence.BasicModel.ItemOptionLink { ItemOption = socket, Level = 1 });
        Assert.That(factory.GetPowerUps(item, player.Attributes!).Sum(power => power.Value), Is.EqualTo(baseline));
        Assert.That(item.ItemOptions, Has.Count.EqualTo(1), "Runtime ignores legacy options without corrupting or deleting the item.");
        Assert.That(SocketOptionPolicy.IsActive(item, ItemOptionTypes.SocketOption), Is.False);
        Assert.That(SocketOptionPolicy.IsActive(item, ItemOptionTypes.SocketBonusOption), Is.False);
        Assert.That(SocketOptionPolicy.IsActive(item, ItemOptionTypes.Option), Is.True);
        Assert.That(this._configuration.ItemOptionCombinationBonuses.Any(bonus => bonus.Requirements.Any(requirement => SocketOptionPolicy.IsSocketOption(requirement.OptionType))), Is.False);
    }

    [TestCase(22, 150, 165)]
    [TestCase(23, 200, 220)]
    public void CustomMacesUseRealBlessAndSoulUpgradeLogic(int number, int minimum, int maximum)
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var definition = this._configuration.Items.Single(item => item.Group == 2 && item.Number == number);
        var item = new TemporaryItem { Definition = definition, Durability = definition.Durability };
        var bless = new ExposedBless();
        for (byte level = 1; level <= 6; level++)
        {
            Assert.That(bless.Upgrade(item, context), Is.True);
            Assert.That(item.Level, Is.EqualTo(level));
        }

        Assert.That(bless.Upgrade(item, context), Is.False, "Bless cannot upgrade past +6.");
        var random = new Mock<IRandomizer>();
        random.Setup(randomizer => randomizer.NextRandomBool(It.IsAny<int>())).Returns(true);
        var soul = new ExposedSoul(random.Object);
        for (byte level = 7; level <= 9; level++)
        {
            Assert.That(soul.Upgrade(item, context), Is.True);
            Assert.That(item.Level, Is.EqualTo(level));
        }

        Assert.That(soul.Upgrade(item, context), Is.False, "Soul cannot upgrade past +9.");
        foreach (var (attribute, baseDamage) in new[] { (Stats.MinimumPhysBaseDmgByWeapon, minimum), (Stats.MaximumPhysBaseDmgByWeapon, maximum) })
        {
            var power = definition.BasePowerUpAttributes.Single(power => power.TargetAttribute == attribute);
            Assert.That(power.BaseValue + power.BonusPerLevelTable!.BonusPerLevel.Single(bonus => bonus.Level == item.Level).AdditionalValue, Is.EqualTo(baseDamage + 4 * item.Level));
        }
    }

    [Test]
    public void ReapplyingOnlyChangesChocolateAndPreservesStarAndOtherBoxes()
    {
        var preserved = this.UnrelatedBoxFingerprint();
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        new ChocolateEndgameRewards(context, this._configuration).Initialize();
        new ChocolateEndgameItemBalance(context, this._configuration).Initialize();
        new DisabledSocketSystem(context, this._configuration).Initialize();
        new RewardCategoryRatios(context, this._configuration).Initialize();
        Assert.That(this.UnrelatedBoxFingerprint(), Is.EqualTo(preserved));
    }

    [Test]
    public void ExportOptionalContractSnapshot()
    {
        var path = Environment.GetEnvironmentVariable("CHOCOLATE_SNAPSHOT_PATH");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var groups = this.Groups(32).Concat(this.Groups(33)).Concat(this.Groups(34)).ToArray();
        var snapshot = new
        {
            Rewards = groups.Select(group => new { Description = group.Description.Value, group.Chance, group.MinimumLevel, group.MaximumLevel, group.SkillChance, group.LuckChance, Items = group.PossibleItems.Select(item => new[] { (int)item.Group, (int)item.Number }) }),
            Items = groups.SelectMany(group => group.PossibleItems).Distinct().OrderBy(item => item.Group).ThenBy(item => item.Number).Select(item => new
            {
                item.Group, item.Number, Name = item.Name.Value, item.DropLevel, item.Durability, item.MaximumSockets,
                Stats = item.BasePowerUpAttributes.Where(power => power.TargetAttribute is not null).ToDictionary(power => power.TargetAttribute!.Designation!, power => power.BaseValue),
                Requirements = item.Requirements.ToDictionary(requirement => requirement.Attribute!.Designation!, requirement => requirement.MinimumValue),
            }),
        };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private string UnrelatedBoxFingerprint() => string.Join('\n', this._configuration.Items.Where(item => !(item.Group == 14 && item.Number is 32 or 33 or 34))
        .OrderBy(item => item.Group).ThenBy(item => item.Number).SelectMany(item => item.DropItems.Select(group => $"{item.Group}/{item.Number}:{group.SourceItemLevel}:{group.Description}:{group.Chance}:{group.MinimumLevel}:{group.MaximumLevel}:{group.ItemType}:{group.MoneyAmount}:{group.SkillChance}:{group.LuckChance}:{string.Join(',', group.PossibleItems.Select(candidate => $"{candidate.Group}/{candidate.Number}"))}")));

    private List<ItemDropItemGroup> Groups(int number) => this._configuration.Items.Single(item => item.Group == 14 && item.Number == number).DropItems.Where(group => group.SourceItemLevel == 0).ToList();

    private double Category(IEnumerable<ItemDropItemGroup> groups, string category) => groups.Where(group => group.Description.Value!.Contains($"/ {category} /", StringComparison.Ordinal)).Sum(group => group.Chance);

    private sealed class ExposedBless : BlessJewelConsumeHandlerPlugIn
    {
        public bool Upgrade(MUnique.OpenMU.DataModel.Entities.Item item, IContext context) => this.ModifyItem(item, context);
    }

    private sealed class ExposedSoul(IRandomizer randomizer) : SoulJewelConsumeHandlerPlugIn(randomizer)
    {
        public bool Upgrade(MUnique.OpenMU.DataModel.Entities.Item item, IContext context) => this.ModifyItem(item, context);
    }
}
