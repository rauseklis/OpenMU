// <copyright file="PotionStackingTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Verifies recovery potion stack boundaries supplied by item configuration.
/// </summary>
[TestFixture]
public class PotionStackingTest
{
    /// <summary>
    /// The configured capacity supports complete and partial merges without overflow.
    /// </summary>
    /// <param name="capacity">The item stack limit.</param>
    [TestCase(50)]
    [TestCase(3)]
    public void ConfiguredStackCapacityIsRespected(int capacity)
    {
        var definition = new ItemDefinition { Group = 14, Number = 1, Durability = (short)capacity };
        var source = new Item { Definition = definition, Durability = 1 };
        var target = new Item { Definition = definition, Durability = capacity - 1 };
        Assert.That(source.CanCompletelyStackOn(target), Is.True);

        source.Durability = capacity;
        Assert.That(source.CanCompletelyStackOn(target), Is.False);
        Assert.That(source.CanPartiallyStackOn(target), Is.True);

        target.Durability = capacity;
        Assert.That(source.CanPartiallyStackOn(target), Is.False);
    }

    /// <summary>
    /// Potion type and item level must match before a stack can merge.
    /// </summary>
    [Test]
    public void DifferentPotionTypesAndLevelsCannotMerge()
    {
        var definition = new ItemDefinition { Group = 14, Number = 1, Durability = 50 };
        var source = new Item { Definition = definition, Durability = 1 };
        var target = new Item { Definition = definition, Durability = 1, Level = 1 };
        Assert.That(source.CanPartiallyStackOn(target), Is.False);
        target.Level = 0;
        target.Definition = new ItemDefinition { Group = 14, Number = 4, Durability = 50 };
        Assert.That(source.CanPartiallyStackOn(target), Is.False);
    }
}
