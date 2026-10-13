// <copyright file="AuctionTaxonomyTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.IO;
using System.Text.RegularExpressions;
using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the browse taxonomy: the category and sub-category of an item by group and number, the sub-category filter
/// rules, and a name-based cross-check of every entry of the owner's Item.txt.
/// </summary>
[TestFixture]
public class AuctionTaxonomyTests
{
    /// <summary>
    /// Verifies known items classify as the spec's taxonomy table says.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="category">The expected category.</param>
    /// <param name="subCategory">The expected sub-category.</param>
    [TestCase(0, 16, AuctionCategory.Weapon, 0)]
    [TestCase(1, 0, AuctionCategory.Weapon, 1)]
    [TestCase(2, 5, AuctionCategory.Weapon, 2)]
    [TestCase(3, 0, AuctionCategory.Weapon, 3)]
    [TestCase(4, 6, AuctionCategory.Weapon, 4)]
    [TestCase(4, 7, AuctionCategory.Weapon, 4)]
    [TestCase(5, 10, AuctionCategory.Weapon, 5)]
    [TestCase(7, 8, AuctionCategory.Armor, 0)]
    [TestCase(8, 8, AuctionCategory.Armor, 1)]
    [TestCase(9, 8, AuctionCategory.Armor, 2)]
    [TestCase(10, 8, AuctionCategory.Armor, 3)]
    [TestCase(11, 8, AuctionCategory.Armor, 4)]
    [TestCase(6, 8, AuctionCategory.Armor, 5)]
    [TestCase(12, 5, AuctionCategory.Wing, 0)]
    [TestCase(12, 150, AuctionCategory.Wing, 0)]
    [TestCase(12, 40, AuctionCategory.Wing, 1)]
    [TestCase(12, 242, AuctionCategory.Wing, 1)]
    [TestCase(13, 30, AuctionCategory.Wing, 1)]
    [TestCase(13, 4, AuctionCategory.PetHelper, 0)]
    [TestCase(13, 5, AuctionCategory.PetHelper, 0)]
    [TestCase(13, 3, AuctionCategory.PetHelper, 1)]
    [TestCase(13, 37, AuctionCategory.PetHelper, 1)]
    [TestCase(13, 400, AuctionCategory.PetHelper, 1)]
    [TestCase(13, 8, AuctionCategory.PetHelper, 2)]
    [TestCase(13, 12, AuctionCategory.PetHelper, 3)]
    [TestCase(13, 200, AuctionCategory.PetHelper, 4)]
    [TestCase(13, 46, AuctionCategory.PetHelper, 5)]
    [TestCase(14, 13, AuctionCategory.JewelMaterial, 0)]
    [TestCase(12, 15, AuctionCategory.JewelMaterial, 0)]
    [TestCase(14, 160, AuctionCategory.JewelMaterial, 0)]
    [TestCase(14, 250, AuctionCategory.JewelMaterial, 0)]
    [TestCase(12, 30, AuctionCategory.JewelMaterial, 1)]
    [TestCase(12, 141, AuctionCategory.JewelMaterial, 1)]
    [TestCase(14, 41, AuctionCategory.JewelMaterial, 2)]
    [TestCase(14, 44, AuctionCategory.JewelMaterial, 2)]
    [TestCase(12, 60, AuctionCategory.JewelMaterial, 3)]
    [TestCase(12, 129, AuctionCategory.JewelMaterial, 3)]
    [TestCase(13, 15, AuctionCategory.JewelMaterial, 4)]
    [TestCase(13, 57, AuctionCategory.JewelMaterial, 4)]
    [TestCase(14, 3, AuctionCategory.Consumable, 0)]
    [TestCase(14, 133, AuctionCategory.Consumable, 0)]
    [TestCase(14, 72, AuctionCategory.Consumable, 1)]
    [TestCase(14, 140, AuctionCategory.Consumable, 1)]
    [TestCase(14, 11, AuctionCategory.Consumable, 2)]
    [TestCase(14, 300, AuctionCategory.Consumable, 2)]
    [TestCase(12, 7, AuctionCategory.ScrollOrb, 0)]
    [TestCase(12, 44, AuctionCategory.ScrollOrb, 0)]
    [TestCase(12, 21, AuctionCategory.ScrollOrb, 1)]
    [TestCase(12, 48, AuctionCategory.ScrollOrb, 1)]
    [TestCase(15, 0, AuctionCategory.ScrollOrb, 1)]
    [TestCase(15, 36, AuctionCategory.ScrollOrb, 1)]
    [TestCase(12, 26, AuctionCategory.Miscellaneous, 0)]
    [TestCase(16, 0, AuctionCategory.Miscellaneous, 0)]
    [TestCase(20, 3, AuctionCategory.Miscellaneous, 0)]
    public void ClassifiesKnownItems(int group, int number, AuctionCategory category, int subCategory)
    {
        var result = AuctionTaxonomy.Classify(group, number);

        Assert.Multiple(() =>
        {
            Assert.That(result.Category, Is.EqualTo(category));
            Assert.That((int)result.SubCategory, Is.EqualTo(subCategory));
        });
    }

    /// <summary>
    /// Verifies a sub-category is only a filter when a specific category and a specific sub-category are both named.
    /// </summary>
    /// <param name="category">The requested category, 255 for all.</param>
    /// <param name="subCategory">The requested sub-category, 255 for all.</param>
    /// <param name="expected">The expected filter, or <c>null</c> for no filter.</param>
    [TestCase((byte)255, (byte)3, null)]
    [TestCase((byte)1, (byte)255, null)]
    [TestCase((byte)255, (byte)255, null)]
    [TestCase((byte)1, (byte)3, 3)]
    [TestCase((byte)0, (byte)0, 0)]
    public void SubCategoryFilterIsIgnoredWithoutACategory(byte category, byte subCategory, int? expected)
    {
        var filter = AuctionTaxonomy.ToSubCategoryFilter(category, subCategory);

        Assert.That(filter, Is.EqualTo(expected is null ? null : (byte?)expected));
    }

    /// <summary>
    /// Cross-checks the rules against the names in the owner's Item.txt: an item named like a ring is a ring, an orb is an
    /// orb, a cape is a cape, and so on. A failure lists each item the rules put in the wrong place.
    /// </summary>
    [Test]
    public void EveryCatalogEntryLandsWhereItsNameSays()
    {
        var catalog = LoadCatalog();
        Assert.That(catalog, Has.Count.GreaterThan(1500));
        var violations = new List<string>();

        void Expect(string label, Func<int, string, bool> applies, Func<int, int, string, (AuctionCategory, int)> expected)
        {
            foreach (var (group, number, name) in catalog)
            {
                if (!applies(group, name))
                {
                    continue;
                }

                var actual = AuctionTaxonomy.Classify(group, number);
                var wanted = expected(group, number, name);
                if (actual.Category != wanted.Item1 || actual.SubCategory != wanted.Item2)
                {
                    violations.Add($"{label}: ({group},{number}) '{name}' is {actual.Category}/{actual.SubCategory}, expected {wanted.Item1}/{wanted.Item2}");
                }
            }
        }

        Expect("orb or scroll", (g, n) => (g == 12 || g == 15) && Regex.IsMatch(n, "Orb|Scroll|Parchment") && !Regex.IsMatch(n, "Seed|Sphere"), (g, _, n) => (AuctionCategory.ScrollOrb, n.Contains("Orb") ? 0 : 1));
        Expect("crystal", (g, n) => g == 12 && n.StartsWith("Crystal of", StringComparison.Ordinal), (_, _, _) => (AuctionCategory.ScrollOrb, 0));
        Expect("cape or cloak", (g, n) => g == 12 && Regex.IsMatch(n, "Cape|Cloak"), (_, _, _) => (AuctionCategory.Wing, 1));
        Expect("wings", (g, n) => g == 12 && Regex.IsMatch(n, "Wing|Wind of") && !Regex.IsMatch(n, "Cape|Cloak"), (_, _, _) => (AuctionCategory.Wing, 0));
        Expect("ring", (g, n) => g == 13 && Regex.IsMatch(n, @"Ring\b"), (_, _, _) => (AuctionCategory.PetHelper, 2));
        Expect("pendant", (g, n) => g == 13 && Regex.IsMatch(n, "Pendant|Necklace"), (_, _, _) => (AuctionCategory.PetHelper, 3));
        Expect("egg", (g, n) => g == 13 && n.StartsWith("Egg ", StringComparison.Ordinal), (_, _, _) => (AuctionCategory.PetHelper, 4));
        Expect("bundle", (g, n) => g == 12 && n.StartsWith("Bundled", StringComparison.Ordinal), (_, _, _) => (AuctionCategory.JewelMaterial, 1));
        Expect("seed or sphere", (g, n) => g == 12 && Regex.IsMatch(n, "^(Seed|Sphere)"), (_, _, _) => (AuctionCategory.JewelMaterial, 3));
        Expect("jewel", (g, n) => (g == 12 || g == 14) && Regex.IsMatch(n, "^Jewel [Oo]f"), (_, _, _) => (AuctionCategory.JewelMaterial, 0));
        Expect("potion", (g, n) => g == 14 && n.Contains("Potion"), (_, _, _) => (AuctionCategory.Consumable, 0));
        Expect("elixir", (g, n) => g == 14 && n.StartsWith("Elixir of", StringComparison.Ordinal), (_, _, _) => (AuctionCategory.Consumable, 1));

        Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
    }

    private static List<(int Group, int Number, string Name)> LoadCatalog()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "AuctionHouse", "Fixtures", "ItemCatalog.tsv");
        return File.ReadAllLines(path)
            .Where(line => line.Length > 0)
            .Select(line => line.Split('\t'))
            .Select(parts => (int.Parse(parts[0]), int.Parse(parts[1]), parts[2]))
            .ToList();
    }
}
