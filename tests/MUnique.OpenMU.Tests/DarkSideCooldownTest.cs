// <copyright file="DarkSideCooldownTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Diagnostics;
using System.Threading;
using MUnique.OpenMU.GameLogic.PlayerActions.Skills;

/// <summary>
/// Tests the server-side Dark Side cast interval.
/// </summary>
[TestFixture]
public class DarkSideCooldownTest
{
    [Test]
    public void EnforcesOneSecondAndDoesNotExtendOnRejectedCast()
    {
        var gate = new DarkSideCooldown();
        Assert.That(gate.TryBegin(0), Is.True);
        Assert.That(gate.TryBegin(Stopwatch.Frequency - 1), Is.False);
        Assert.That(gate.TryBegin(Stopwatch.Frequency), Is.True);
        Assert.That(gate.TryBegin(Stopwatch.Frequency), Is.False);
        Assert.That(gate.TryBegin(Stopwatch.Frequency * 2), Is.True);
    }

    [Test]
    public void ConcurrentCastsOnlyAcceptOne()
    {
        var gate = new DarkSideCooldown();
        var accepted = 0;
        Parallel.For(0, 100, _ =>
        {
            if (gate.TryBegin(0))
            {
                Interlocked.Increment(ref accepted);
            }
        });
        Assert.That(accepted, Is.EqualTo(1));
    }

    [TestCase(263, true)]
    [TestCase(559, true)]
    [TestCase(262, false)]
    [TestCase(264, false)]
    public void RecognizesOnlyDarkSideAndItsUpgrade(int skillId, bool expected)
    {
        Assert.That(DarkSideCooldown.IsDarkSide((ushort)skillId), Is.EqualTo(expected));
    }
}
