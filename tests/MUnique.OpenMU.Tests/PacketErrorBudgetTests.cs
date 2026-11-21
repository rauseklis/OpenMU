// <copyright file="PacketErrorBudgetTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// Tests the limit on what a connection can cost with packets which make a handler fail.
/// </summary>
[TestFixture]
public class PacketErrorBudgetTests
{
    private static readonly DateTime Start = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Only the first failures of a minute are logged in detail; the rest are suppressed.</summary>
    [Test]
    public void OnlyTheFirstFailuresOfAMinuteAreLoggedInDetail()
    {
        var budget = new PacketErrorBudget();

        var decisions = Enumerable.Range(0, 10).Select(i => budget.Record(Start.AddSeconds(i), 1000, 3)).ToList();

        Assert.That(decisions.Count(d => d.LogDetail), Is.EqualTo(3));
        Assert.That(decisions.Take(3).All(d => d.LogDetail), Is.True);
        Assert.That(decisions.Skip(3).All(d => !d.LogDetail), Is.True);
    }

    /// <summary>A new minute allows detailed logging again and reports how many failures were suppressed in the minute before.</summary>
    [Test]
    public void NewMinuteStartsANewBudgetAndReportsTheSuppressedCount()
    {
        var budget = new PacketErrorBudget();
        for (var i = 0; i < 10; i++)
        {
            budget.Record(Start.AddSeconds(i), 1000, 3);
        }

        var next = budget.Record(Start.AddSeconds(61), 1000, 3);

        Assert.That(next.LogDetail, Is.True);
        Assert.That(next.SuppressedInPreviousWindow, Is.EqualTo(7));
        Assert.That(next.ErrorsInWindow, Is.EqualTo(1));
    }

    /// <summary>The connection is reported as exceeding exactly once per minute, when it crosses the limit.</summary>
    [Test]
    public void ExceedingTheLimitIsReportedOncePerMinute()
    {
        var budget = new PacketErrorBudget();

        var exceeded = Enumerable.Range(0, 20).Select(i => budget.Record(Start.AddSeconds(i), 10, 3)).Select(d => d.Exceeded).ToList();

        Assert.That(exceeded.Count(e => e), Is.EqualTo(1));
        Assert.That(exceeded[10], Is.True, "the eleventh failure crosses a limit of ten");
        Assert.That(budget.Record(Start.AddSeconds(70), 10, 3).Exceeded, Is.False, "a new minute starts counting from zero");
    }

    /// <summary>A connection which stays below the limit is never reported.</summary>
    [Test]
    public void StayingBelowTheLimitIsNeverReported()
    {
        var budget = new PacketErrorBudget();

        var decisions = Enumerable.Range(0, 10).Select(i => budget.Record(Start.AddSeconds(i * 10), 10, 3)).ToList();

        Assert.That(decisions.Any(d => d.Exceeded), Is.False);
    }

    /// <summary>The compiled defaults only observe, the limits are generous.</summary>
    [Test]
    public void DefaultsObserveOnly()
    {
        var configuration = new AntiCheatConfiguration();

        Assert.That(configuration.PacketErrorMode, Is.EqualTo(AntiCheatMode.Shadow));
        Assert.That(configuration.PacketErrorMaximumPerMinute, Is.GreaterThanOrEqualTo(50));
        Assert.That(configuration.PacketErrorDetailLogsPerMinute, Is.InRange(1, 20));
    }
}
