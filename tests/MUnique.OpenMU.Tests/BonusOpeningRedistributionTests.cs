// <copyright file="BonusOpeningRedistributionTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Pins the approved opening chances of Silver Medal, Gold Medal, Heart of Love, Firecracker and Box of Heaven.
/// </summary>
[TestFixture]
public class BonusOpeningRedistributionTests
{
    private const byte Firecracker = 2;
    private const byte Heart = 3;
    private const byte Silver = 5;
    private const byte Gold = 6;
    private const byte Heaven = 7;

    private GameConfiguration _configuration = null!;

    /// <summary>
    /// Creates a fresh Season 6 configuration once.
    /// </summary>
    /// <returns>The task.</returns>
    [OneTimeSetUp]
    public async Task InitializeAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        this._configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
    }

    /// <summary>
    /// Every bonus box keeps summing to exactly 100%.
    /// </summary>
    /// <param name="level">The bonus level.</param>
    [TestCase(Silver)]
    [TestCase(Gold)]
    [TestCase(Heart)]
    [TestCase(Firecracker)]
    [TestCase(Heaven)]
    public void EveryBoxSumsToOneHundredPercent(byte level)
    {
        Assert.That(this.Groups(level).Sum(group => group.Chance), Is.EqualTo(1).Within(1e-12));
    }

    /// <summary>
    /// Each jewel is 1%, rings and Zen are kept, and the equipment shares match the approved plan.
    /// </summary>
    /// <param name="level">The bonus level.</param>
    /// <param name="jewels">The number of jewels.</param>
    /// <param name="rings">The ring total.</param>
    /// <param name="zen">The Zen total.</param>
    /// <param name="armor">The armor total.</param>
    /// <param name="weapons">The weapon total.</param>
    /// <param name="shields">The shield total.</param>
    [TestCase(Silver, 3, .05, .20, .42, .20, .10)]
    [TestCase(Gold, 4, .05, .10, .51, .20, .10)]
    [TestCase(Heart, 4, 0, .10, 0, .774, .086)]
    [TestCase(Firecracker, 5, 0, .25, .56, .14, 0)]
    public void CategoryTotalsMatchThePlan(byte level, int jewels, double rings, double zen, double armor, double weapons, double shields)
    {
        var groups = this.Groups(level);
        Assert.That(Total(groups, "jewels"), Is.EqualTo(jewels * .01).Within(1e-12));
        Assert.That(Total(groups, "rings"), Is.EqualTo(rings).Within(1e-12));
        Assert.That(groups.Where(group => group.ItemType == SpecialItemType.Money).Sum(group => group.Chance), Is.EqualTo(zen).Within(1e-12));
        Assert.That(Equipment(groups, item => item.Group is >= 7 and <= 11), Is.EqualTo(armor).Within(1e-12));
        Assert.That(Equipment(groups, item => item.Group < 6 && !(item.Group == 2 && item.Number == 22)), Is.EqualTo(weapons).Within(1e-12));
        Assert.That(Equipment(groups, item => item.Group == 6), Is.EqualTo(shields).Within(1e-12));
    }

    /// <summary>
    /// Every jewel of every changed box is exactly 1%.
    /// </summary>
    /// <param name="level">The bonus level.</param>
    [TestCase(Silver)]
    [TestCase(Gold)]
    [TestCase(Heart)]
    [TestCase(Firecracker)]
    public void EveryJewelIsOnePercent(byte level)
    {
        foreach (var group in this.Groups(level).Where(group => Category(group) == "jewels"))
        {
            foreach (var item in group.PossibleItems)
            {
                Assert.That(group.Chance / group.PossibleItems.Count, Is.EqualTo(.01).Within(1e-12), $"level {level}, jewel {item.Group}/{item.Number}");
            }
        }
    }

    /// <summary>
    /// The equal item shares inside a category are kept: every armor set, weapon and shield of a box is equally likely.
    /// </summary>
    /// <param name="level">The bonus level.</param>
    /// <param name="armorSet">The chance of one armor set (0 if the box has no armor).</param>
    /// <param name="weapon">The chance of one weapon.</param>
    /// <param name="shield">The chance of one shield (0 if the box has no shield).</param>
    [TestCase(Silver, .07, .20 / 11, .025)]
    [TestCase(Gold, .085, .20 / 12, .025)]
    [TestCase(Heart, 0, .774 / 15, .086)]
    [TestCase(Firecracker, .08, .14 / 8, 0)]
    public void ItemsInsideACategoryStayEquallyLikely(byte level, double armorSet, double weapon, double shield)
    {
        var groups = this.Groups(level);
        foreach (var group in groups.Where(group => group.PossibleItems.Count > 0))
        {
            var chance = group.Chance / group.PossibleItems.Count;
            var first = group.PossibleItems.First();
            if (Category(group) is "jewels" or "rings")
            {
                continue;
            }

            if (first.Group is >= 7 and <= 11)
            {
                Assert.That(group.Chance, Is.EqualTo(armorSet).Within(1e-12), $"level {level}, armor set {first.Number}");
            }
            else if (first.Group == 6)
            {
                Assert.That(chance, Is.EqualTo(shield).Within(1e-12), $"level {level}, shield {first.Number}");
            }
            else
            {
                Assert.That(chance, Is.EqualTo(weapon).Within(1e-12), $"level {level}, weapon {first.Group}/{first.Number}");
            }
        }
    }

    /// <summary>
    /// Box of Heaven: every higher pet 1%, ordinary pets share the rest of the old pet chance.
    /// </summary>
    [Test]
    public void HeavenHasOnePercentPerHigherPet()
    {
        var groups = this.Groups(Heaven);
        Assert.That(groups.Single(group => Category(group) == "jewels").Chance, Is.EqualTo(.45).Within(1e-12));
        Assert.That(groups.Single(group => Category(group) == "pets").Chance, Is.EqualTo(.32).Within(1e-12));
        var higher = groups.Single(group => Category(group) == "higher pets");
        Assert.That(higher.Chance / higher.PossibleItems.Count, Is.EqualTo(.01).Within(1e-12));
        Assert.That(higher.PossibleItems.Select(item => (item.Group, item.Number)), Is.EquivalentTo(new[] { ((byte)13, (short)64), ((byte)13, (short)65), ((byte)13, (short)3) }));
        var zen = groups.Single(group => group.ItemType == SpecialItemType.Money);
        Assert.That(zen.Chance, Is.EqualTo(.20).Within(1e-12));
        Assert.That(zen.MoneyAmount, Is.EqualTo(100_000));
    }

    /// <summary>
    /// Gold Medal (0.35%), Heart of Love (0.30%) and Box of Heaven (0.05%) change their monster chance; Silver, Firecracker and Star keep theirs.
    /// </summary>
    [Test]
    public void OnlyGoldHeartAndHeavenChangeTheirMonsterChance()
    {
        var expected = new Dictionary<byte, double> { [1] = .0005, [2] = .0015, [3] = .003, [5] = .01, [6] = .0035, [7] = .0005 };
        var seen = new HashSet<byte>();
        foreach (var group in this._configuration.Maps.SelectMany(map => map.DropItemGroups).Where(group => group.ItemType == SpecialItemType.SmallBonus))
        {
            Assert.That(group.Chance, Is.EqualTo(expected[group.ItemLevel!.Value]).Within(1e-12), group.Description);
            seen.Add(group.ItemLevel!.Value);
        }

        Assert.That(seen, Is.EquivalentTo(expected.Keys));
    }

    /// <summary>
    /// Star and every other box are not touched.
    /// </summary>
    [Test]
    public async Task OnlyTheFiveBonusBoxesChangeAndReapplyingIsIdempotentAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var untouched = Fingerprint(configuration, includeChanged: false);
        var all = Fingerprint(configuration, includeChanged: true);

        new BonusOpeningRedistribution(context, configuration).Initialize();

        Assert.That(Fingerprint(configuration, includeChanged: false), Is.EqualTo(untouched));
        Assert.That(Fingerprint(configuration, includeChanged: true), Is.EqualTo(all), "Reapplication must be idempotent.");
    }

    /// <summary>
    /// A hundred thousand actual openings of Silver Medal follow the approved per-item chances.
    /// </summary>
    [Test]
    public void OneHundredThousandSilverOpeningsFollowTheApprovedChances()
    {
        const int samples = 100_000;
        var groups = this.Groups(Silver);
        var generator = new DefaultDropGenerator(this._configuration, new SeededRandomizer(23112026));
        var expected = groups.Where(group => group.PossibleItems.Count > 0).SelectMany(group => group.PossibleItems.Select(item => (Key: (item.Group, item.Number), Chance: group.Chance / group.PossibleItems.Count)))
            .GroupBy(row => row.Key).ToDictionary(rows => rows.Key, rows => rows.Sum(row => row.Chance));
        var observed = expected.Keys.ToDictionary(key => key, _ => 0);
        var zen = 0;
        for (var draw = 0; draw < samples; draw++)
        {
            var result = generator.GenerateItemDrop(groups);
            if (result.Money is not null)
            {
                zen++;
                continue;
            }

            var item = result.Item!;
            observed[(item.Definition!.Group, item.Definition.Number)]++;
        }

        foreach (var (key, chance) in expected)
        {
            var tolerance = Math.Max(12, 6 * Math.Sqrt(samples * chance * (1 - chance)));
            Assert.That(observed[key], Is.EqualTo(samples * chance).Within(tolerance), $"Silver item {key}");
        }

        Assert.That((double)zen / samples, Is.EqualTo(.20).Within(.005));
    }

    /// <summary>
    /// An existing database receives the new chances exactly once and later edits are kept.
    /// </summary>
    [Test]
    public async Task StartupInstallerUpdatesAnExistingDatabaseOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var wrapper = configuration.Items.Single(item => item.Group == 14 && item.Number == 11);
        var silverJewels = wrapper.DropItems.Single(group => group.SourceItemLevel == Silver && Category(group) == "jewels");
        silverJewels.Chance = .2;
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.BonusOpeningRedistribution).ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await BonusOpeningRedistributionInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(silverJewels.Chance, Is.EqualTo(.03).Within(1e-12));

        silverJewels.Chance = .5;
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await BonusOpeningRedistributionInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        Assert.That(silverJewels.Chance, Is.EqualTo(.5));
    }

    private static string Category(ItemDropItemGroup group)
    {
        var description = group.Description.Value ?? string.Empty;
        return description[(description.LastIndexOf(" / ", StringComparison.Ordinal) + 3)..];
    }

    private static double Total(IEnumerable<ItemDropItemGroup> groups, string category) => groups.Where(group => Category(group) == category).Sum(group => group.Chance);

    private static double Equipment(IEnumerable<ItemDropItemGroup> groups, Func<ItemDefinition, bool> selector) =>
        groups.Where(group => group.PossibleItems.Count > 0 && group.PossibleItems.All(selector)).Sum(group => group.Chance);

    private static string Fingerprint(GameConfiguration configuration, bool includeChanged) => string.Join('\n', configuration.Items.OrderBy(item => item.Group).ThenBy(item => item.Number)
        .SelectMany(item => item.DropItems.Where(group => IsChanged(item, group) == includeChanged)
            .Select(group => $"{item.Group}/{item.Number}:{group.SourceItemLevel}:{group.Description.Value}:{group.Chance:R}:{group.ItemType}:{group.MinimumLevel}:{group.MaximumLevel}:{group.MoneyAmount}:{string.Join(',', group.PossibleItems.Select(reward => $"{reward.Group}/{reward.Number}").Order())}")));

    private static bool IsChanged(ItemDefinition item, ItemDropItemGroup group) => item.Group == 14 && item.Number == 11 && group.SourceItemLevel is Firecracker or Heart or Silver or Gold or Heaven;

    private ItemDropItemGroup[] Groups(byte level) => this._configuration.Items.Single(item => item.Group == 14 && item.Number == 11)
        .DropItems.Where(group => group.SourceItemLevel == level).ToArray();
}
