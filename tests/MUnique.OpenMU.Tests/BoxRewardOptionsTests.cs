// <copyright file="BoxRewardOptionsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Tests independent box option rolls and preservation of shared item options.
/// </summary>
[TestFixture]
public class BoxRewardOptionsTests
{
    /// <summary>
    /// Exactly a quarter of red-box rolls have luck while equipment ADD stays unchanged.
    /// </summary>
    [Test]
    public void BoxLuckUsesExactQuarterWithoutChangingAdd()
    {
        var item = CreateItem();
        var luck = CreateOptions(ItemOptionTypes.Luck, 1);
        item.Definition!.PossibleItemOptions.Add(luck);
        var add = new ItemOptionLink { ItemOption = new IncreasableItemOption { OptionType = ItemOptionTypes.Option }, Level = 2 };
        item.ItemOptions.Add(add);
        var randomizer = new Mock<IRandomizer>();
        double roll = 0;
        randomizer.Setup(random => random.NextDouble()).Returns(() => roll);
        var group = new ItemDropItemGroup { LuckChance = 0.25 };
        var lucky = 0;
        const int samples = 1000;
        for (var index = 0; index < samples; index++)
        {
            roll = (index + 0.5) / samples;
            BoxRewardOptions.Apply(item, group, randomizer.Object);
            lucky += item.ItemOptions.Count(link => link.ItemOption?.OptionType == ItemOptionTypes.Luck);
            Assert.That(item.ItemOptions, Does.Contain(add));
            Assert.That(add.Level, Is.EqualTo(2));
        }

        Assert.That(lucky, Is.EqualTo(250));
    }

    /// <summary>
    /// Verifies each tier's exact one/two-option proportions over a deterministic grid.
    /// </summary>
    /// <param name="chance">The second-option chance.</param>
    [TestCase(0.20)]
    [TestCase(0.30)]
    [TestCase(0.25)]
    [TestCase(0.15)]
    [TestCase(0.10)]
    public void ExcellentDistributionPreservesLuckAndAdd(double chance)
    {
        var item = CreateItem();
        var excellent = CreateOptions(ItemOptionTypes.Excellent, 6);
        item.Definition!.PossibleItemOptions.Add(excellent);
        var luck = new ItemOptionLink { ItemOption = new IncreasableItemOption { OptionType = ItemOptionTypes.Luck } };
        var add = new ItemOptionLink { ItemOption = new IncreasableItemOption { OptionType = ItemOptionTypes.Option }, Level = 2 };
        item.ItemOptions.Add(luck);
        item.ItemOptions.Add(add);
        var group = new ItemDropItemGroup { ItemType = SpecialItemType.Excellent, ExcellentSecondOptionChance = chance };
        var randomizer = new Mock<IRandomizer>();
        double roll = 0;
        randomizer.Setup(r => r.NextDouble()).Returns(() => roll);
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns((int minimum, int maximum) => minimum);
        var secondOptions = 0;
        const int samples = 1000;
        for (var index = 0; index < samples; index++)
        {
            roll = (index + 0.5) / samples;
            BoxRewardOptions.Apply(item, group, randomizer.Object);
            var selected = item.ItemOptions.Where(link => link.ItemOption?.OptionType == ItemOptionTypes.Excellent).ToList();
            Assert.That(selected.Count, Is.InRange(1, 2));
            Assert.That(selected.Select(link => link.ItemOption).Distinct().Count(), Is.EqualTo(selected.Count));
            Assert.That(item.ItemOptions, Does.Contain(luck));
            Assert.That(item.ItemOptions, Does.Contain(add));
            Assert.That(add.Level, Is.EqualTo(2));
            secondOptions += selected.Count - 1;
        }

        Assert.That(secondOptions, Is.EqualTo((int)(samples * chance)));
    }

    /// <summary>
    /// Verifies exact skill boundaries, support checks and Excellent's guaranteed skill.
    /// </summary>
    /// <param name="chance">The skill chance.</param>
    /// <param name="roll">The random roll.</param>
    /// <param name="supported">Whether the item supports skill.</param>
    /// <param name="excellent">Whether the reward is Excellent.</param>
    /// <param name="expected">The expected skill flag.</param>
    [TestCase(0.5, 0.499, true, false, true)]
    [TestCase(0.5, 0.5, true, false, false)]
    [TestCase(0.9, 0.899, true, false, true)]
    [TestCase(0.9, 0.9, true, false, false)]
    [TestCase(1.0, 0, false, false, false)]
    [TestCase(0, 0.99, true, true, true)]
    [TestCase(1.0, 0, false, true, false)]
    public void SkillRespectsSupportAndRewardType(double chance, double roll, bool supported, bool excellent, bool expected)
    {
        var item = CreateItem(supported);
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextDouble()).Returns(roll);
        var group = new ItemDropItemGroup { SkillChance = chance, ItemType = excellent ? SpecialItemType.Excellent : SpecialItemType.RandomItem };
        BoxRewardOptions.Apply(item, group, randomizer.Object);
        Assert.That(item.HasSkill, Is.EqualTo(expected));
    }

    /// <summary>
    /// Verifies guaranteed jewelry options, including an existing option, without duplicates.
    /// </summary>
    /// <param name="mana">Whether to use maximum mana instead of HP recovery.</param>
    /// <param name="level">The selected option level.</param>
    [TestCase(false, 1)]
    [TestCase(false, 2)]
    [TestCase(false, 3)]
    [TestCase(true, 1)]
    [TestCase(true, 2)]
    [TestCase(true, 3)]
    public void JewelryAlwaysReceivesSupportedOption(bool mana, byte level)
    {
        var item = CreateItem();
        item.Definition!.Group = 13;
        var options = CreateOptions(ItemOptionTypes.Option, 1);
        var option = options.PossibleOptions.Single();
        option.PowerUpDefinition = new PowerUpDefinition { TargetAttribute = mana ? Stats.MaximumMana : Stats.HealthRecoveryMultiplier };
        item.Definition.PossibleItemOptions.Add(options);
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(1, 4)).Returns(level);
        var group = new ItemDropItemGroup { GuaranteeJewelryOption = true };
        BoxRewardOptions.Apply(item, group, randomizer.Object);
        BoxRewardOptions.Apply(item, group, randomizer.Object);
        Assert.That(item.ItemOptions.Count, Is.EqualTo(1));
        Assert.That(item.ItemOptions.Single().Level, Is.EqualTo(level));
        Assert.That(item.ItemOptions.Single().ItemOption, Is.SameAs(option));
    }

    /// <summary>
    /// Verifies that old reward groups leave generated options intact.
    /// </summary>
    [Test]
    public void UnconfiguredGroupPreservesGeneratedItem()
    {
        var item = CreateItem(true);
        item.HasSkill = true;
        var option = new ItemOptionLink { Level = 3 };
        item.ItemOptions.Add(option);
        var randomizer = new Mock<IRandomizer>(MockBehavior.Strict);
        BoxRewardOptions.Apply(item, new ItemDropItemGroup(), randomizer.Object);
        Assert.That(item.HasSkill, Is.True);
        Assert.That(item.ItemOptions.Single(), Is.SameAs(option));
    }

    private static TemporaryItem CreateItem(bool skillSupported = false)
    {
        var definition = new Mock<ItemDefinition>();
        definition.SetupAllProperties();
        definition.Setup(d => d.PossibleItemOptions).Returns(new List<ItemOptionDefinition>());
        definition.Setup(d => d.QualifiedCharacters).Returns(new List<CharacterClass> { new() });
        definition.Object.ItemSlot = new ItemSlotType();
        definition.Object.Skill = skillSupported ? new Skill() : null;
        return new TemporaryItem { Definition = definition.Object };
    }

    private static ItemOptionDefinition CreateOptions(ItemOptionType type, int count)
    {
        var definition = new Mock<ItemOptionDefinition>();
        definition.Setup(d => d.PossibleOptions).Returns(Enumerable.Range(0, count)
            .Select(index => new IncreasableItemOption { Number = index, OptionType = type }).ToList());
        return definition.Object;
    }
}
