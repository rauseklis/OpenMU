// <copyright file="JewelRainConfigurationTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.JewelRain;

using MUnique.OpenMU.GameLogic.PlugIns.JewelRain;

/// <summary>
/// Tests the default values of the <see cref="JewelRainConfiguration"/>.
/// </summary>
[TestFixture]
public class JewelRainConfigurationTest
{
    /// <summary>
    /// The default configuration matches the agreed design.
    /// </summary>
    [Test]
    public void DefaultMatchesTheDesign()
    {
        var config = JewelRainConfiguration.Default;

        Assert.That(config.Enabled, Is.True);
        Assert.That(config.Days, Is.EqualTo(new[] { DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Sunday }));
        Assert.That(config.StartTimes, Is.EqualTo(new[] { new TimeOnly(21, 0) }));
        Assert.That(config.CountdownMinutes, Is.EqualTo(new[] { 5, 4, 3, 2, 1 }));
        Assert.That(config.MapNumber, Is.EqualTo(2));
        Assert.That(config.StepInterval, Is.EqualTo(TimeSpan.FromSeconds(30)));
        Assert.That(config.EndDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
        Assert.That(config.Steps, Has.Count.EqualTo(5));
        Assert.That(config.Steps.All(s => s.Amount == 15), Is.True);

        // Chaos, Soul, Bless, Creation, Life - ids as defined by the item initialization.
        Assert.That(config.Steps.Select(s => (s.Group, s.Number)), Is.EqualTo(new[] { ((byte)12, (short)15), ((byte)14, (short)14), ((byte)14, (short)13), ((byte)14, (short)22), ((byte)14, (short)16) }));

        // The Devias town area.
        Assert.That((config.AreaX1, config.AreaY1, config.AreaX2, config.AreaY2), Is.EqualTo(((byte)178, (byte)6, (byte)244, (byte)74)));
    }
}
