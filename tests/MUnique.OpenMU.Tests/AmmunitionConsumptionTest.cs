// <copyright file="AmmunitionConsumptionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Verifies finite and unlimited ammunition consumption settings.
/// </summary>
[TestFixture]
public class AmmunitionConsumptionTest
{
    /// <summary>
    /// A zero consumption rate preserves even the last arrow across repeated hits.
    /// </summary>
    /// <param name="amount">The initial ammunition quantity.</param>
    [TestCase(1)]
    [TestCase(255)]
    public void UnlimitedAmmunitionSurvivesRepeatedAttacks(int amount)
    {
        var attacker = CreateAttacker(amount, 0);
        for (var attack = 0; attack < 1000; attack++)
        {
            attacker.ApplyAmmunitionConsumption(new HitInfo(10, 0, DamageAttributes.Undefined));
        }

        Assert.That(attacker.Attributes[Stats.AmmunitionAmount], Is.EqualTo(amount));
    }

    /// <summary>
    /// Finite ammunition and reflected hits retain their existing behavior.
    /// </summary>
    /// <param name="amount">The initial quantity.</param>
    /// <param name="rate">The amount consumed per attack.</param>
    /// <param name="damageAttributes">The hit attributes.</param>
    /// <param name="expected">The remaining quantity.</param>
    [TestCase(2, 1, DamageAttributes.Undefined, 1)]
    [TestCase(1, 1, DamageAttributes.Undefined, 0)]
    [TestCase(0, 1, DamageAttributes.Undefined, 0)]
    [TestCase(1, 2, DamageAttributes.Undefined, 1)]
    [TestCase(2, 1, DamageAttributes.Reflected, 2)]
    public void ConfiguredConsumptionIsRespected(int amount, int rate, DamageAttributes damageAttributes, int expected)
    {
        var attacker = CreateAttacker(amount, rate);
        attacker.ApplyAmmunitionConsumption(new HitInfo(10, 0, damageAttributes));
        Assert.That(attacker.Attributes[Stats.AmmunitionAmount], Is.EqualTo(expected));
    }

    private static IAttacker CreateAttacker(int amount, int rate)
    {
        var attributes = new AttributeSystem(
            new IAttribute[]
            {
                new StatAttribute(Stats.AmmunitionAmount, amount),
                new StatAttribute(Stats.AmmunitionConsumptionRate, rate),
            },
            Array.Empty<IAttribute>(),
            Array.Empty<AttributeRelationship>());
        var attacker = new Mock<IAttacker>();
        attacker.SetupGet(value => value.Attributes).Returns(attributes);
        return attacker.Object;
    }
}
