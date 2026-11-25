// <copyright file="FruitPointsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Resets;

/// <summary>
/// Tests the fruit limit and how fruit points are remembered per stat, so that a reset keeps them.
/// </summary>
[TestFixture]
public class FruitPointsTests
{
    /// <summary>
    /// The limit is the same for every character, independent of its level.
    /// </summary>
    [Test]
    public void MaximumFruitPointsAreThreeThousand()
    {
        Assert.That(new Character().GetMaximumFruitPoints(), Is.EqualTo(3000));
        Assert.That(CharacterExtensions.MaximumFruitPoints, Is.EqualTo(3000));
    }

    /// <summary>
    /// Added and removed fruit points are tracked per stat, never go below zero and ignore other attributes.
    /// </summary>
    [Test]
    public void FruitPointsAreTrackedPerStat()
    {
        var character = new Character();
        character.AddStatFruitPoints(Stats.BaseStrength, 5);
        character.AddStatFruitPoints(Stats.BaseAgility, 3);
        character.AddStatFruitPoints(Stats.BaseVitality, 2);
        character.AddStatFruitPoints(Stats.BaseEnergy, 4);
        character.AddStatFruitPoints(Stats.BaseLeadership, 1);
        character.AddStatFruitPoints(Stats.BaseStrength, -2);
        character.AddStatFruitPoints(Stats.BaseAgility, -10);
        character.AddStatFruitPoints(Stats.Level, 7);

        Assert.That(character.GetStatFruitPoints(Stats.BaseStrength), Is.EqualTo(3));
        Assert.That(character.GetStatFruitPoints(Stats.BaseAgility), Is.Zero);
        Assert.That(character.GetStatFruitPoints(Stats.BaseVitality), Is.EqualTo(2));
        Assert.That(character.GetStatFruitPoints(Stats.BaseEnergy), Is.EqualTo(4));
        Assert.That(character.GetStatFruitPoints(Stats.BaseLeadership), Is.EqualTo(1));
        Assert.That(character.GetStatFruitPoints(Stats.Level), Is.Zero);
    }

    /// <summary>
    /// A reset keeps the fruit points, but never more than the stat has above its start value.
    /// </summary>
    /// <param name="invested">The points above the start value.</param>
    /// <param name="fruit">The fruit points.</param>
    /// <param name="expectedKept">The expected kept points.</param>
    [TestCase(100, 40, 40)]
    [TestCase(100, 0, 0)]
    [TestCase(100, 100, 100)]
    [TestCase(30, 40, 30)]
    [TestCase(0, 40, 0)]
    [TestCase(-5, 40, 0)]
    [TestCase(100, -3, 0)]
    public void ResetKeepsOnlyFruitPoints(int invested, int fruit, int expectedKept)
    {
        Assert.That(ResetCharacterAction.CalculateKeptFruitPoints(invested, fruit), Is.EqualTo(expectedKept));
    }
}
