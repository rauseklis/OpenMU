// <copyright file="KundunProgressionTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

/// <summary>
/// Validates the pre-Pink Excellent reward progression.
/// </summary>
[TestFixture]
public class KundunProgressionTests
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
    public void ExcellentChancesAndPrePinkBoundary()
    {
        var pinkBlue = this._configuration.Items.Where(item => item.Group == 14 && item.Number is 32 or 34)
            .SelectMany(item => item.DropItems.Where(group => group.SourceItemLevel == 0)).SelectMany(group => group.PossibleItems).ToHashSet();
        for (var tier = 1; tier <= 5; tier++)
        {
            var groups = this._configuration.Items.Single(item => item.Group == 14 && item.Number == 11)
                .DropItems.Where(group => group.SourceItemLevel == tier + 7).ToArray();
            Assert.That(groups.All(group => group.ExcellentSecondOptionChance == (6 - tier) / 10.0), Is.True, $"Kundun +{tier}");
            Assert.That(groups.All(group => group.ItemType == SpecialItemType.Excellent && group.MinimumLevel == 0 && group.MaximumLevel == 0), Is.True);
            Assert.That(groups.SelectMany(group => group.PossibleItems).Any(pinkBlue.Contains), Is.False, "No Pink or Blue item is legal in Kundun.");
        }
    }

    [TestCase(1, "59FCEF4DF47509E9A2FD8152F24D01CB9524DBEC81BD31D72835732189F50D70")]
    [TestCase(2, "EA4535380C95EF116A6D06EB7DF16619439A791D8AB21760FFF61F9E903FE12A")]
    [TestCase(3, "74A241CC968687FDF2C40EB461CB637BE55D78880324754BFE195D380D19FF6A")]
    [TestCase(4, "E2081A0D82DD4A01E5310402B2AF35FB0E36BBD450A25C6DB6A85D5B937A534D")]
    [TestCase(5, "0EF2BF284A22169CB1D6D7BEEC6C71A97858ECB191F5C6AE3E27774BAF546C1C")]
    public void ExactTierItemsMatchApprovedTables(int tier, string expectedHash)
    {
        var rows = this.Groups(tier).SelectMany(group => group.PossibleItems)
            .Select(item => $"{item.Group}/{item.Number}").Distinct().Order(StringComparer.Ordinal);
        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', rows));
        Assert.That(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), Is.EqualTo(expectedHash));
    }

    [Test]
    public void CategoryWeightsAndLegalPiecesAreExact()
    {
        for (var tier = 1; tier <= 5; tier++)
        {
            var groups = this.Groups(tier);
            foreach (var (category, chance) in new[] { ("armor", .6), ("weapons", .3), ("shields", .1) })
            {
                var categoryGroups = groups.Where(group => group.Description.Value!.Split(" / ")[1] == category).ToArray();
                Assert.That(categoryGroups.Sum(group => group.Chance), Is.EqualTo(chance).Within(1e-12));
                foreach (var group in categoryGroups)
                {
                    Assert.That(group.PossibleItems.All(item => item.QualifiedCharacters.Count > 0), Is.True);
                    Assert.That(group.SkillChance, Is.Null);
                    Assert.That(group.LuckChance, Is.Null);
                }
            }
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void OneHundredThousandOpeningsHaveOneExcellentRewardAndCorrectOptions(int tier)
    {
        const int samples = 100_000;
        var groups = this.Groups(tier);
        var generator = new DefaultDropGenerator(this._configuration, new SeededRandomizer(20261026 + tier));
        var categories = new int[3];
        var doubleOptions = 0;
        for (var draw = 0; draw < samples; draw++)
        {
            var result = generator.GenerateItemDrop(groups);
            Assert.That(result.Money, Is.Null);
            Assert.That(result.Item, Is.Not.Null);
            var item = result.Item!;
            Assert.That(item.Level, Is.Zero);
            Assert.That(item.SocketCount, Is.Zero);
            Assert.That(item.IsExcellent(), Is.True);
            Assert.That(item.IsAncient(), Is.False);
            var options = item.ItemOptions.Where(link => link.ItemOption?.OptionType == ItemOptionTypes.Excellent).Select(link => link.ItemOption).ToArray();
            Assert.That(options.Length, Is.InRange(1, 2));
            Assert.That(options.Distinct().Count(), Is.EqualTo(options.Length));
            doubleOptions += options.Length == 2 ? 1 : 0;
            categories[item.Definition!.Group >= 7 ? 0 : item.Definition.Group == 6 ? 2 : 1]++;
        }

        Assert.That((double)doubleOptions / samples, Is.EqualTo((6 - tier) / 10.0).Within(.005));
        for (var category = 0; category < categories.Length; category++)
        {
            Assert.That((double)categories[category] / samples, Is.EqualTo(new[] { .6, .3, .1 }[category]).Within(.005));
        }

        TestContext.WriteLine($"Kundun +{tier}: armor/weapons/shields {categories[0]}/{categories[1]}/{categories[2]}, two distinct EXC {doubleOptions}/{samples}.");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void ExcellentOptionRollIsIndependentOfItemSelection(int tier)
    {
        const int samples = 100_000;
        var group = this.Groups(tier).First();
        var randomizer = new SeededRandomizer(12000 + tier);
        var doubles = 0;
        for (var draw = 0; draw < samples; draw++)
        {
            var item = new TemporaryItem { Definition = group.PossibleItems.First() };
            BoxRewardOptions.Apply(item, group, randomizer);
            var options = item.ItemOptions.Where(link => link.ItemOption?.OptionType == ItemOptionTypes.Excellent).ToArray();
            Assert.That(options.Length, Is.InRange(1, 2));
            Assert.That(options.Select(link => link.ItemOption).Distinct().Count(), Is.EqualTo(options.Length));
            doubles += options.Length == 2 ? 1 : 0;
        }

        Assert.That((double)doubles / samples, Is.EqualTo((6 - tier) / 10.0).Within(.005));
        TestContext.WriteLine($"Kundun +{tier} independent EXC-only: {doubles}/{samples} two distinct options.");
    }

    [Test]
    public void OnlyKundunPoolsChangeAndReapplyIsIdempotent()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var context = provider.CreateNewContext();
        var before = this.UnrelatedFingerprint();
        var expected = this.KundunFingerprint();
        new KundunProgression(context, this._configuration).Initialize();
        new UniqueRewardWeights(context, this._configuration).Initialize();
        Assert.That(this.UnrelatedFingerprint(), Is.EqualTo(before));
        Assert.That(this.KundunFingerprint(), Is.EqualTo(expected));
        for (var tier = 1; tier <= 3; tier++)
        {
            Assert.That(this.Groups(tier).SelectMany(group => group.PossibleItems).Any(item => item.Group == 5 && item.Number == 20 + tier), Is.True);
        }

        Assert.That(this.Groups(1).SelectMany(group => group.PossibleItems).Any(item => item.Group >= 7 && item.QualifiedCharacters.Any(character => character.Number / 4 == 5)), Is.False);
        Assert.That(this.Groups(2).SelectMany(group => group.PossibleItems).Any(item => item.Group == 8 && item.Number == 39), Is.True);
        Assert.That(this.Groups(5).SelectMany(group => group.PossibleItems).Any(item => item.Group == 8 && item.Number == 42), Is.True);
    }

    private List<ItemDropItemGroup> Groups(int tier) => this._configuration.Items.Single(item => item.Group == 14 && item.Number == 11).DropItems.Where(group => group.SourceItemLevel == tier + 7).ToList();

    private string KundunFingerprint() => string.Join('\n', Enumerable.Range(1, 5).SelectMany(this.Groups).Select(GroupFingerprint).Order());

    private string UnrelatedFingerprint() => string.Join('\n', this._configuration.Items.SelectMany(item => item.DropItems.Where(group => !(item.Group == 14 && item.Number == 11 && group.SourceItemLevel is >= 8 and <= 12))).Select(GroupFingerprint).Order());

    private static string GroupFingerprint(ItemDropItemGroup group) => $"{group.SourceItemLevel}:{group.Description}:{group.Chance}:{group.ItemType}:{group.MinimumLevel}:{group.MaximumLevel}:{group.ExcellentSecondOptionChance}:{group.LuckChance}:{group.SkillChance}:{group.MoneyAmount}:{string.Join(',', group.PossibleItems.Select(item => $"{item.Group}/{item.Number}").Order())}";

    [Test]
    public void ExportCatalogWhenRequested()
    {
        if (Environment.GetEnvironmentVariable("KUNDUN_CATALOG_PATH") is not { } path)
        {
            return;
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this._configuration.Items.Where(item => item.Group < 12).Select(item => new
        {
            item.Group,
            item.Number,
            Name = item.Name.Value,
            Classes = item.QualifiedCharacters.Select(character => new { character.Number, Name = character.Name.Value }),
            Options = item.PossibleItemOptions.SelectMany(options => options.PossibleOptions).Select(option => option.OptionType?.Name.Value).Distinct(),
        }), new JsonSerializerOptions { WriteIndented = true }));
    }
}
