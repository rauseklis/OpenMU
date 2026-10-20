// <copyright file="JewelBankKindsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.JewelBank;

using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// Tests the catalog of the kinds which the Jewel Bank accepts.
/// </summary>
[TestFixture]
public class JewelBankKindsTests
{
    /// <summary>
    /// Kind codes are unique and never zero, because they are stored and sent on the wire.
    /// </summary>
    [Test]
    public void CodesAreUniqueAndNonZero()
    {
        var codes = JewelBankKinds.All.Select(k => k.Code).ToList();
        Assert.That(codes, Is.Unique);
        Assert.That(codes, Does.Not.Contain((byte)0));
        Assert.That(codes, Has.Count.EqualTo(15));
    }

    /// <summary>
    /// A plain jewel is found by group, number and level 0 only; an upgraded jewel is not bankable.
    /// </summary>
    [Test]
    public void LooseJewelIsFoundAtLevelZeroOnly()
    {
        Assert.That(JewelBankKinds.Find(14, 13, 0)?.Name, Is.EqualTo("Jewel of Bless"));
        Assert.That(JewelBankKinds.Find(14, 13, 1), Is.Null);
        Assert.That(JewelBankKinds.Find(12, 15, 0)?.Name, Is.EqualTo("Jewel of Chaos"));
    }

    /// <summary>
    /// A fruit is identified by its level; levels beyond the five fruit types are not bankable.
    /// </summary>
    [Test]
    public void FruitsAreSeparatedByLevel()
    {
        var names = Enumerable.Range(0, 5).Select(level => JewelBankKinds.Find(13, 15, level)?.Name).ToList();
        Assert.That(names, Is.EqualTo(new[] { "Fruit of Energy", "Fruit of Vitality", "Fruit of Agility", "Fruit of Strength", "Fruit of Command" }));
        Assert.That(JewelBankKinds.Find(13, 15, 5), Is.Null);
    }

    /// <summary>
    /// Items which the bank does not know are never accepted.
    /// </summary>
    [Test]
    public void UnknownItemsAreRejected()
    {
        Assert.That(JewelBankKinds.Find(0, 0, 0), Is.Null);
        Assert.That(JewelBankKinds.Find(14, 99, 0), Is.Null);
        Assert.That(JewelBankKinds.FindByCode(0), Is.Null);
        Assert.That(JewelBankKinds.FindByCode(200), Is.Null);
    }

    /// <summary>
    /// A withdrawal is handed out as the largest packs first, then loose items.
    /// </summary>
    [TestCase(65, true, new byte[] { 2, 2 }, 5)]
    [TestCase(10, true, new byte[] { 0 }, 0)]
    [TestCase(9, true, new byte[0], 9)]
    [TestCase(60, true, new byte[] { 2, 2 }, 0)]
    [TestCase(20, true, new byte[] { 1 }, 0)]
    [TestCase(65, false, new byte[0], 65)]
    public void SplitUsesLargestPacksFirst(long units, bool canPack, byte[] packs, int loose)
    {
        var (actualPacks, actualLoose) = JewelBankKinds.Split(units, canPack);

        Assert.That(actualPacks, Is.EqualTo(packs));
        Assert.That(actualLoose, Is.EqualTo(loose));
    }

    /// <summary>
    /// The split never creates or loses units.
    /// </summary>
    [Test]
    public void SplitPreservesTheTotal()
    {
        for (var units = 1; units < 500; units++)
        {
            var (packs, loose) = JewelBankKinds.Split(units, true);
            Assert.That(packs.Sum(p => JewelBankKinds.GetPackUnits(p)) + loose, Is.EqualTo(units));
        }
    }
}
