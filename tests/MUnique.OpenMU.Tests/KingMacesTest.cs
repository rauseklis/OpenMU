// <copyright file="KingMacesTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;
using Basic = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Verifies staged mace definitions and their isolated upgrade progression.
/// </summary>
[TestFixture]
public class KingMacesTest
{
    [TestCase(22, 150, 165, 30)]
    [TestCase(23, 200, 220, 40)]
    public void CreatesApprovedWeaponWithoutChangingTemplate(int number, int minimum, int maximum, int speed)
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var config = context.CreateNew<Basic.GameConfiguration>();
        foreach (var attribute in new[] { Stats.MinimumPhysBaseDmgByWeapon, Stats.MaximumPhysBaseDmgByWeapon,
                     Stats.AttackSpeedByWeapon, Stats.EquippedWeaponCount, Stats.DoubleWieldWeaponCount,
                     Stats.TotalStrengthRequirementValue, Stats.TotalAgilityRequirementValue })
        {
            config.Attributes.Add(context.CreateNew<AttributeDefinition>(attribute.Id, attribute.Designation!, attribute.Description!));
        }

        for (byte i = 0; i < 28; i++)
        {
            var cls = context.CreateNew<Basic.CharacterClass>();
            cls.Number = i;
            config.CharacterClasses.Add(cls);
        }

        var template = context.CreateNew<Basic.ItemDefinition>();
        template.Group = 0;
        template.Number = 16;
        template.Name = "Sword of Destruction";
        template.DropLevel = 82;
        template.Durability = 84;
        template.MaximumItemLevel = 15;
        template.ItemSlot = context.CreateNew<Basic.ItemSlotType>();
        template.ItemSlot.ItemSlots.Add(0);
        template.ItemSlot.ItemSlots.Add(1);
        config.Items.Add(template);
        var initializer = new KingMaces(context, config);
        initializer.Initialize();
        initializer.Initialize();
        Assert.That(config.Items.Count, Is.EqualTo(3));
        Assert.That(config.ItemLevelBonusTables.Count, Is.EqualTo(1));
        Assert.That(template.BasePowerUpAttributes, Is.Empty);
        var item = config.Items.Single(i => i.Group == 2 && i.Number == number);
        Assert.That(item.Skill, Is.Null);
        var instance = context.CreateNew<Basic.Item>();
        instance.Definition = item;
        Assert.That(instance.CanHaveSkill(), Is.False, "Normal, excellent and ancient drops must remain skill-free.");
        var strength = item.Requirements.Single(r => r.Attribute == Stats.TotalStrengthRequirementValue);
        var agility = item.Requirements.Single(r => r.Attribute == Stats.TotalAgilityRequirementValue);
        var strengthCoefficient = number == 22 ? 480 : 968;
        var agilityCoefficient = number == 22 ? 334 : 675;
        Assert.That(strength.MinimumValue, Is.EqualTo(strengthCoefficient));
        Assert.That(agility.MinimumValue, Is.EqualTo(agilityCoefficient));
        for (byte level = 0; level <= 13; level++)
        {
            instance.Level = level;
            Assert.That(instance.GetRequirement(strength).Value, Is.EqualTo(20 + (strengthCoefficient * (82 + (3 * level)) * 3 / 100)));
            Assert.That(instance.GetRequirement(agility).Value, Is.EqualTo(20 + (agilityCoefficient * (82 + (3 * level)) * 3 / 100)));
        }

        instance.Level = 0;
        var option = context.CreateNew<Basic.ItemOptionLink>();
        option.ItemOption = context.CreateNew<Basic.IncreasableItemOption>();
        option.ItemOption.OptionType = ItemOptionTypes.Option;
        instance.ItemOptions.Add(option);
        var baseStrength = instance.GetRequirement(strength).Value;
        var baseAgility = instance.GetRequirement(agility).Value;
        for (byte optionLevel = 1; optionLevel <= 7; optionLevel++)
        {
            option.Level = optionLevel;
            Assert.That(instance.GetRequirement(strength).Value, Is.EqualTo(baseStrength + (optionLevel * 5)));
            Assert.That(instance.GetRequirement(agility).Value, Is.EqualTo(baseAgility));
        }

        strength.MinimumValue = 160;
        item.Skill = context.CreateNew<Basic.Skill>();
        new KingMaceRequirements(context, config).Initialize();
        Assert.That(strength.MinimumValue, Is.EqualTo(strengthCoefficient), "Existing definitions must be upgraded too.");
        Assert.That(item.Skill, Is.Null);
        Assert.That(item.Width, Is.EqualTo(1));
        Assert.That(item.Height, Is.EqualTo(3));
        Assert.That(item.DropsFromMonsters, Is.False);
        Assert.That(item.QualifiedCharacters.Select(c => c.Number / 4).Distinct(), Is.EquivalentTo(new[] { 1, 3, 4 }));
        Assert.That(item.BasePowerUpAttributes.Single(p => p.TargetAttribute == Stats.AttackSpeedByWeapon).BaseValue, Is.EqualTo(speed));
        Assert.That(item.BasePowerUpAttributes.Any(p => p.TargetAttribute == Stats.IsTwoHandedWeaponEquipped
            || p.TargetAttribute == Stats.IsTwoHandedStaffEquipped || p.TargetAttribute == Stats.ScepterRise), Is.False);
        var min = item.BasePowerUpAttributes.Single(p => p.TargetAttribute == Stats.MinimumPhysBaseDmgByWeapon);
        var max = item.BasePowerUpAttributes.Single(p => p.TargetAttribute == Stats.MaximumPhysBaseDmgByWeapon);
        for (var level = 0; level <= 15; level++)
        {
            var bonus = min.BonusPerLevelTable!.BonusPerLevel.SingleOrDefault(b => b.Level == level)?.AdditionalValue ?? 0;
            Assert.That(min.BaseValue + bonus, Is.EqualTo(minimum + (4 * level)));
            Assert.That(max.BaseValue + bonus, Is.EqualTo(maximum + (4 * level)));
        }
    }
}
