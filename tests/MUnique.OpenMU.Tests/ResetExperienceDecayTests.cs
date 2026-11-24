// <copyright file="ResetExperienceDecayTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Tests the experience decay with resets (500x server: 500x until 5 resets, then 10x less per reset, at least 50x).
/// </summary>
[TestFixture]
public class ResetExperienceDecayTests
{
    /// <summary>
    /// Tests the resulting experience rate for some reset counts.
    /// </summary>
    /// <param name="resets">The reset count.</param>
    /// <param name="expectedRate">The expected experience rate.</param>
    [TestCase(0, 500)]
    [TestCase(5, 500)]
    [TestCase(6, 490)]
    [TestCase(10, 450)]
    [TestCase(20, 350)]
    [TestCase(40, 150)]
    [TestCase(49, 60)]
    [TestCase(50, 50)]
    [TestCase(80, 50)]
    public void RateFollowsTheResetCount(int resets, double expectedRate)
    {
        var configuration = new ResetExperienceDecayConfiguration();

        var rate = ResetExperienceDecayPlugIn.GetFactor(configuration, resets) * configuration.BaseRate;

        Assert.That(rate, Is.EqualTo(expectedRate).Within(0.001));
    }

    /// <summary>
    /// Tests that the default configuration only applies to the 500x server (id 0).
    /// </summary>
    [Test]
    public void DefaultsTargetTheFiveHundredServer()
    {
        var configuration = (ResetExperienceDecayConfiguration)new ResetExperienceDecayPlugIn().CreateDefaultConfig();

        Assert.That(configuration.OnlyServerId, Is.EqualTo((byte)0));
        Assert.That(configuration.BaseRate, Is.EqualTo(500));
        Assert.That(configuration.ResetsWithoutDecay, Is.EqualTo(5));
        Assert.That(configuration.DecreasePerReset, Is.EqualTo(10));
        Assert.That(configuration.MinimumRate, Is.EqualTo(50));
    }

    /// <summary>
    /// Tests that nothing is calculated without a configuration or with an invalid base rate.
    /// </summary>
    [Test]
    public void InvalidBaseRateChangesNothing()
    {
        var configuration = new ResetExperienceDecayConfiguration { BaseRate = 0 };

        Assert.That(ResetExperienceDecayPlugIn.GetFactor(configuration, 30), Is.EqualTo(1.0));
    }
}
