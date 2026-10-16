// <copyright file="TwoHandedWeaponBonusTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Verifies the two-handed bonus without changing ancient bonuses or dual wield.
/// </summary>
[TestFixture]
public class TwoHandedWeaponBonusTest
{
    [TestCase(1, 0, DamageType.Physical, 1150)]
    [TestCase(0, 1, DamageType.Wizardry, 1150)]
    [TestCase(0, 1, DamageType.Curse, 1150)]
    [TestCase(1, 1, DamageType.Physical, 1150)]
    [TestCase(0, 0, DamageType.Physical, 1000)]
    [TestCase(0, 0, DamageType.Wizardry, 1000)]
    [TestCase(1, 0, DamageType.Fenrir, 1000)]
    [TestCase(0, 1, DamageType.SummonedMonster, 1000)]
    public void GrantsFifteenPercentOnlyForTwoHandedWeaponFlags(int melee, int staff, DamageType type, int expected)
    {
        var attributes = new Mock<IAttributeSystem>();
        attributes.Setup(x => x[Stats.IsTwoHandedWeaponEquipped]).Returns(melee);
        attributes.Setup(x => x[Stats.IsTwoHandedStaffEquipped]).Returns(staff);
        attributes.Setup(x => x[Stats.HasDoubleWield]).Returns(1);
        attributes.Setup(x => x[Stats.IsBowEquipped]).Returns(1);
        Assert.That(AttackableExtensions.ApplyTwoHandedWeaponBonus(1000, attributes.Object, type), Is.EqualTo(expected));
    }
}
