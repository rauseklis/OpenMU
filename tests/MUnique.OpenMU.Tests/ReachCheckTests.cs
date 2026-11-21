// <copyright file="ReachCheckTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// Tests the reach check of melee hits.
/// </summary>
[TestFixture]
public class ReachCheckTests
{
    /// <summary>A hit within the reach is never reported or refused, in any mode.</summary>
    [Test]
    public void HitWithinReachIsNeverRefused()
    {
        foreach (var mode in new[] { AntiCheatMode.Off, AntiCheatMode.Shadow, AntiCheatMode.Enforce })
        {
            var configuration = new AntiCheatConfiguration { HitReachMode = mode, HitReachMaximumDistance = 10 };
            Assert.That(ReachCheck.ShouldRefuse(configuration, NullLogger.Instance, "acct", 2), Is.False, mode.ToString());
            Assert.That(ReachCheck.ShouldRefuse(configuration, NullLogger.Instance, "acct", 10), Is.False, "exactly at the limit is within reach");
        }
    }

    /// <summary>A hit beyond the reach is only observed in shadow mode, refused in enforce mode, ignored when off.</summary>
    [Test]
    public void HitBeyondReachIsRefusedOnlyInEnforceMode()
    {
        var shadow = new AntiCheatConfiguration { HitReachMode = AntiCheatMode.Shadow, HitReachMaximumDistance = 10 };
        var enforce = new AntiCheatConfiguration { HitReachMode = AntiCheatMode.Enforce, HitReachMaximumDistance = 10 };
        var off = new AntiCheatConfiguration { HitReachMode = AntiCheatMode.Off, HitReachMaximumDistance = 10 };

        Assert.That(ReachCheck.ShouldRefuse(shadow, NullLogger.Instance, "acct", 30), Is.False, "shadow only observes");
        Assert.That(ReachCheck.ShouldRefuse(enforce, NullLogger.Instance, "acct", 30), Is.True);
        Assert.That(ReachCheck.ShouldRefuse(off, NullLogger.Instance, "acct", 30), Is.False);
    }

    /// <summary>A drop beyond the reach is only observed in shadow mode, refused in enforce mode; a close drop never.</summary>
    [Test]
    public void DropBeyondReachIsRefusedOnlyInEnforceMode()
    {
        var shadow = new AntiCheatConfiguration { DropReachMode = AntiCheatMode.Shadow, DropReachMaximumDistance = 8 };
        var enforce = new AntiCheatConfiguration { DropReachMode = AntiCheatMode.Enforce, DropReachMaximumDistance = 8 };
        var off = new AntiCheatConfiguration { DropReachMode = AntiCheatMode.Off, DropReachMaximumDistance = 8 };

        Assert.That(ReachCheck.ShouldRefuseDrop(shadow, NullLogger.Instance, "acct", 40), Is.False);
        Assert.That(ReachCheck.ShouldRefuseDrop(enforce, NullLogger.Instance, "acct", 40), Is.True);
        Assert.That(ReachCheck.ShouldRefuseDrop(off, NullLogger.Instance, "acct", 40), Is.False);
        Assert.That(ReachCheck.ShouldRefuseDrop(enforce, NullLogger.Instance, "acct", 3), Is.False, "a drop next to the player is fine");
        Assert.That(ReachCheck.ShouldRefuseDrop(enforce, NullLogger.Instance, "acct", 8), Is.False, "exactly at the limit is fine");
    }

    /// <summary>The hit and drop rules are counted and limited separately.</summary>
    [Test]
    public void DefaultsOfTheDropRuleObserveOnly()
    {
        var configuration = new AntiCheatConfiguration();

        Assert.That(configuration.DropReachMode, Is.EqualTo(AntiCheatMode.Shadow));
        Assert.That(configuration.DropReachMaximumDistance, Is.GreaterThanOrEqualTo(6));
    }

    /// <summary>The compiled defaults observe only and are loose enough for ranged weapons.</summary>
    [Test]
    public void DefaultsObserveOnly()
    {
        var configuration = new AntiCheatConfiguration();

        Assert.That(configuration.HitReachMode, Is.EqualTo(AntiCheatMode.Shadow));
        Assert.That(configuration.HitReachMaximumDistance, Is.GreaterThanOrEqualTo(8));
    }
}
