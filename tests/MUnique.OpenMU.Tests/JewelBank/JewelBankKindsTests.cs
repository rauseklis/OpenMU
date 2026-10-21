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
        Assert.That(codes, Has.Count.EqualTo(45));
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
    /// Every jewel which has bundles has all three sizes, and the bundle sizes hold 10, 20 and 30 jewels.
    /// </summary>
    [Test]
    public void EveryJewelHasBundlesOfTenTwentyAndThirty()
    {
        var packs = JewelBankKinds.All.Where(k => k.IsPack).ToList();
        Assert.That(packs, Has.Count.EqualTo(JewelBankKinds.JewelCount * 3));
        Assert.That(packs.Select(k => (k.Number, k.ItemLevel)), Is.Unique);
        Assert.That(packs.Select(k => k.PackUnits).Distinct().Order(), Is.EqualTo(new[] { 10, 20, 30 }));
        Assert.That(JewelBankKinds.Find(12, 141, 0)?.Name, Is.EqualTo("Chaos Bundle"));
        Assert.That(JewelBankKinds.Find(12, 141, 2)?.PackUnits, Is.EqualTo(30));
        Assert.That(JewelBankKinds.Find(12, 143, 1)?.Name, Is.EqualTo("Higher Refining Bundle"));
        Assert.That(JewelBankKinds.Find(12, 141, 3), Is.Null);
    }

    /// <summary>
    /// Only the bundles are packs; jewels and fruits are single units.
    /// </summary>
    [Test]
    public void JewelsAndFruitsAreNotPacks()
    {
        Assert.That(JewelBankKinds.All.Take(15).Any(k => k.IsPack), Is.False);
        Assert.That(JewelBankKinds.All.Skip(15).All(k => k.IsPack), Is.True);
    }
}
