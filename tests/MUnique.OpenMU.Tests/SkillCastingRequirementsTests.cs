// <copyright file="SkillCastingRequirementsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>Checks that removing casting levels never bypasses stat, resource or item requirements.</summary>
[TestFixture]
public class SkillCastingRequirementsTests
{
    /// <summary>Level-one characters can cast only when every required stat is sufficient.</summary>
    /// <param name="stat">The required stat.</param>
    /// <param name="value">The character's stat value.</param>
    /// <param name="allowed">Whether casting should succeed.</param>
    [TestCase("Energy", 219, false)]
    [TestCase("Energy", 220, true)]
    [TestCase("Energy", 221, true)]
    [TestCase("Strength", 219, false)]
    [TestCase("Strength", 220, true)]
    [TestCase("Agility", 219, false)]
    [TestCase("Agility", 220, true)]
    [TestCase("Leadership", 219, false)]
    [TestCase("Leadership", 220, true)]
    public async Task CastingIgnoresLevelButRequiresStatsAsync(string stat, int value, bool allowed)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 1;
        var attribute = stat switch
        {
            "Energy" => Stats.TotalEnergy,
            "Strength" => Stats.TotalStrength,
            "Agility" => Stats.TotalAgility,
            "Leadership" => Stats.TotalLeadership,
            _ => throw new ArgumentOutOfRangeException(nameof(stat)),
        };
        player.Attributes.AddElement(new SimpleElement(value - player.Attributes[attribute], AggregateType.AddRaw), attribute);
        var skill = CreateSkill(new AttributeRequirement { Attribute = Stats.Level, MinimumValue = 400 },
            new AttributeRequirement { Attribute = attribute, MinimumValue = 220 });

        Assert.That(await player.TryConsumeForSkillAsync(new SkillEntry { Skill = skill }).ConfigureAwait(false), Is.EqualTo(allowed));
    }

    /// <summary>Mana and AG are still required and are consumed only on a successful cast.</summary>
    /// <param name="resource">The resource to consume.</param>
    /// <param name="available">The available amount.</param>
    /// <param name="allowed">Whether casting should succeed.</param>
    [TestCase("Mana", 9, false)]
    [TestCase("Mana", 10, true)]
    [TestCase("AG", 9, false)]
    [TestCase("AG", 10, true)]
    public async Task CastingStillConsumesResourcesAsync(string resource, int available, bool allowed)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 1;
        var attribute = resource == "Mana" ? Stats.CurrentMana : Stats.CurrentAbility;
        player.Attributes[attribute] = available;
        var skill = CreateSkill(new AttributeRequirement { Attribute = Stats.Level, MinimumValue = 220 });
        skill.ConsumeRequirements.Add(new AttributeRequirement { Attribute = attribute, MinimumValue = 10 });

        Assert.That(await player.TryConsumeForSkillAsync(new SkillEntry { Skill = skill }).ConfigureAwait(false), Is.EqualTo(allowed));
        Assert.That(player.Attributes[attribute], Is.EqualTo(allowed ? 0 : available));
    }

    /// <summary>Learning or equipping an item still requires its configured character level.</summary>
    /// <param name="requiredLevel">Twisting Slash or Dark Horse level requirement.</param>
    [TestCase(80)]
    [TestCase(218)]
    public async Task LearningAndDarkHorseItemLevelsRemainRequiredAsync(int requiredLevel)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var definition = new Mock<ItemDefinition>();
        definition.Setup(item => item.Requirements).Returns(new List<AttributeRequirement> { new() { Attribute = Stats.Level, MinimumValue = requiredLevel } });
        definition.Setup(item => item.QualifiedCharacters).Returns(new List<CharacterClass> { player.SelectedCharacter!.CharacterClass! });
        var item = new Item { Definition = definition.Object };
        player.Attributes![Stats.Level] = requiredLevel - 1;
        Assert.That(player.CompliesRequirements(item), Is.False);
        player.Attributes[Stats.Level] = requiredLevel;
        Assert.That(player.CompliesRequirements(item), Is.True);
    }

    private static Skill CreateSkill(params AttributeRequirement[] requirements)
    {
        var skill = new Mock<Skill>();
        skill.Setup(value => value.Requirements).Returns(requirements.ToList());
        skill.Setup(value => value.ConsumeRequirements).Returns(new List<AttributeRequirement>());
        skill.Setup(value => value.AttributeRelationships).Returns(new List<AttributeRelationship>());
        return skill.Object;
    }
}
