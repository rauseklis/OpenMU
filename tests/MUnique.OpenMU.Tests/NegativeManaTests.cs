// <copyright file="NegativeManaTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Reflection;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>Checks that mana and AG can never drop below zero.</summary>
[TestFixture]
public class NegativeManaTests
{
    private const int SkillCost = 10;
    private const int CastsAffordable = 5;
    private const int ConcurrentCasts = 200;

    /// <summary>Many casts at the same time never take more mana than the player has.</summary>
    [Test]
    public async Task ConcurrentCastsNeverTakeManaBelowZeroAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.CurrentMana] = SkillCost * CastsAffordable;
        var skill = new Mock<Skill>();
        skill.Setup(value => value.Requirements).Returns(new List<AttributeRequirement>());
        skill.Setup(value => value.ConsumeRequirements).Returns(new List<AttributeRequirement> { new() { Attribute = Stats.CurrentMana, MinimumValue = SkillCost } });
        skill.Setup(value => value.AttributeRelationships).Returns(new List<AttributeRelationship>());
        var entry = new SkillEntry { Skill = skill.Object };

        var results = await Task.WhenAll(Enumerable.Range(0, ConcurrentCasts).Select(_ => Task.Run(async () => await player.TryConsumeForSkillAsync(entry).ConfigureAwait(false)))).ConfigureAwait(false);

        Assert.That(results.Count(allowed => allowed), Is.EqualTo(CastsAffordable));
        Assert.That(player.Attributes[Stats.CurrentMana], Is.Zero);
    }

    /// <summary>Whatever lowers mana, AG, shield or life below zero, the value is set back to zero.</summary>
    /// <param name="name">The name of the tested current value.</param>
    [TestCase("Mana")]
    [TestCase("AG")]
    [TestCase("SD")]
    [TestCase("Life")]
    public async Task ValueBelowZeroIsFlooredAtZeroAsync(string name)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var attributes = player.Attributes!;
        var handler = typeof(Player).GetMethod("OnAttributeValueChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        attributes.AttributeValueChanged += (sender, attribute) => handler.Invoke(player, new object?[] { sender, attribute });
        var definition = name switch
        {
            "Mana" => Stats.CurrentMana,
            "AG" => Stats.CurrentAbility,
            "SD" => Stats.CurrentShield,
            _ => Stats.CurrentHealth,
        };
        attributes[definition] = 5;

        attributes[definition] = -7;

        Assert.That(attributes[definition], Is.Zero);
    }
}
