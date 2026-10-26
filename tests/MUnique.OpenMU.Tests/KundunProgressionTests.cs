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

    [TestCase(1, "F9B2ACD8BE33A1A8847A00E4F341A629E6BEAAF1D2C8196BF113CCDE9FDB7D8D")]
    [TestCase(2, "DD3B4D1BF3EEBAE2588A1E444FF9E03C6996A5ED7632D008FEBE135AA7F07620")]
    [TestCase(3, "B8FCC9787699BF11C837049A7C004072D23ED70BC2CE103C11DACF1F2D5D8E1D")]
    [TestCase(4, "14BD32E926B38E1AEA107E941C14DD5F4251A975137B046AB09912260A12E3B0")]
    [TestCase(5, "0BC9B3EDF3EB71A89A765BC183157E75298D13D24F7A996B23BB1D880EB43B8B")]
    public void ExactTierItemsMatchApprovedTables(int tier, string expectedHash)
    {
        var rows = this.Groups(tier).SelectMany(group =>
        {
            var parts = group.Description.Value!.Split(" / ");
            return group.PossibleItems.Select(item => $"{parts[1]}/{parts[2]}/{item.Group}/{item.Number}");
        }).Order(StringComparer.Ordinal);
        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', rows));
        Assert.That(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), Is.EqualTo(expectedHash));
    }

    [Test]
    public void ClassWeightsAndLegalPiecesAreExact()
    {
        for (var tier = 1; tier <= 5; tier++)
        {
            var groups = this.Groups(tier);
            foreach (var (category, chance, classes) in new[] { ("armor", .6, tier == 1 ? 6 : 7), ("weapons", .3, 7), ("shields", .1, 5) })
            {
                var categoryGroups = groups.Where(group => group.Description.Value!.Split(" / ")[1] == category).ToArray();
                Assert.That(categoryGroups.Sum(group => group.Chance), Is.EqualTo(chance).Within(1e-12));
                var classGroups = categoryGroups.GroupBy(group => group.Description.Value!.Split(" / ")[2]).ToArray();
                Assert.That(classGroups.Length, Is.EqualTo(classes));
                foreach (var classGroup in classGroups)
                {
                    Assert.That(classGroup.Sum(group => group.Chance), Is.EqualTo(chance / classes).Within(1e-12));
                    var family = Array.IndexOf(new[] { "DW", "DK", "Elf", "MG", "DL", "SUM", "RF" }, classGroup.Key);
                    foreach (var group in classGroup)
                    {
                        Assert.That(group.PossibleItems.All(item => item.QualifiedCharacters.Any(character => character.Number / 4 == family)), Is.True);
                        Assert.That(group.SkillChance, Is.Null);
                        Assert.That(group.LuckChance, Is.Null);
                        if (category == "armor")
                        {
                            Assert.That(group.PossibleItems.Select(item => item.Group), Is.EquivalentTo(classGroup.Key == "MG" ? new byte[] { 8, 9, 10, 11 } : classGroup.Key == "RF" ? new byte[] { 7, 8, 9, 11 } : new byte[] { 7, 8, 9, 10, 11 }));
                        }
                    }
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
        Assert.That(this.UnrelatedFingerprint(), Is.EqualTo(before));
        Assert.That(this.KundunFingerprint(), Is.EqualTo(expected));
        for (var tier = 1; tier <= 3; tier++)
        {
            Assert.That(this.Groups(tier).SelectMany(group => group.PossibleItems).Any(item => item.Group == 5 && item.Number == 20 + tier), Is.True);
        }

        Assert.That(this.Groups(1).Any(group => group.Description.Value!.Contains("/ armor / SUM /", StringComparison.Ordinal)), Is.False);
        Assert.That(this.Groups(2).Where(group => group.Description.Value!.Contains("/ armor / SUM /", StringComparison.Ordinal)).SelectMany(group => group.PossibleItems).All(item => item.Number == 39), Is.True);
        Assert.That(this.Groups(5).Where(group => group.Description.Value!.Contains("/ armor / SUM /", StringComparison.Ordinal)).SelectMany(group => group.PossibleItems).All(item => item.Number == 42), Is.True);
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
