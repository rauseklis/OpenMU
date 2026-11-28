// <copyright file="HeroStatusPointsPerLevelTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Tests that the extra level-up point of the 'Gain Hero Status' quest only counts from level 220 on.
/// </summary>
[TestFixture]
public class HeroStatusPointsPerLevelTest
{
    /// <summary>Without the quest bonus, every level gives the class points.</summary>
    /// <param name="level">The reached level.</param>
    [TestCase(2)]
    [TestCase(219)]
    [TestCase(220)]
    [TestCase(400)]
    public void NoBonusGivesClassPoints(int level)
    {
        Assert.That(PlayerExperience.GetPointsForLevelUp(CreateCharacter(5), 5, level), Is.EqualTo(5));
    }

    /// <summary>With the quest bonus, only levels from 220 on give the extra point.</summary>
    /// <param name="level">The reached level.</param>
    /// <param name="expected">The expected points.</param>
    [TestCase(2, 5)]
    [TestCase(219, 5)]
    [TestCase(220, 6)]
    [TestCase(221, 6)]
    [TestCase(400, 6)]
    public void BonusCountsFromLevel220(int level, int expected)
    {
        Assert.That(PlayerExperience.GetPointsForLevelUp(CreateCharacter(5), 6, level), Is.EqualTo(expected));
    }

    /// <summary>Classes with 7 base points (Dark Lord, Magic Gladiator, Rage Fighter) get 7 before and 8 from level 220.</summary>
    /// <param name="level">The reached level.</param>
    /// <param name="expected">The expected points.</param>
    [TestCase(100, 7)]
    [TestCase(219, 7)]
    [TestCase(220, 8)]
    public void BonusWorksForSevenPointClasses(int level, int expected)
    {
        Assert.That(PlayerExperience.GetPointsForLevelUp(CreateCharacter(7), 8, level), Is.EqualTo(expected));
    }

    private static Character CreateCharacter(int basePoints)
    {
        var characterClass = new CharacterClass();
        typeof(CharacterClass).GetProperty(nameof(CharacterClass.StatAttributes))!.SetValue(
            characterClass,
            new List<StatAttributeDefinition> { new(Stats.PointsPerLevelUp, basePoints, false) });
        return new Character { CharacterClass = characterClass };
    }
}
