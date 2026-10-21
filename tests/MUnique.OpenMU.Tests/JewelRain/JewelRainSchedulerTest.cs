// <copyright file="JewelRainSchedulerTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.JewelRain;

using MUnique.OpenMU.GameLogic.PlugIns.JewelRain;

/// <summary>
/// Tests the <see cref="JewelRainScheduler"/> state machine with a fake clock.
/// </summary>
[TestFixture]
public class JewelRainSchedulerTest
{
    // A Wednesday.
    private static readonly DateTime Wednesday = new(2026, 10, 21);

    /// <summary>
    /// The whole run of the default configuration produces exactly the designed timeline.
    /// </summary>
    [Test]
    public void FullRunProducesCountdownStartFiveDropsAndEnd()
    {
        var timeline = Run(JewelRainConfiguration.Default, Wednesday.AddHours(20).AddMinutes(54), Wednesday.AddHours(21).AddMinutes(3));

        var expected = new List<(DateTime, JewelRainAction)>
        {
            (At(20, 55, 0), new(JewelRainActionKind.Countdown, 5)),
            (At(20, 56, 0), new(JewelRainActionKind.Countdown, 4)),
            (At(20, 57, 0), new(JewelRainActionKind.Countdown, 3)),
            (At(20, 58, 0), new(JewelRainActionKind.Countdown, 2)),
            (At(20, 59, 0), new(JewelRainActionKind.Countdown, 1)),
            (At(21, 0, 0), new(JewelRainActionKind.Start, 0)),
            (At(21, 0, 0), new(JewelRainActionKind.Drop, 0)),
            (At(21, 0, 30), new(JewelRainActionKind.Drop, 1)),
            (At(21, 1, 0), new(JewelRainActionKind.Drop, 2)),
            (At(21, 1, 30), new(JewelRainActionKind.Drop, 3)),
            (At(21, 2, 0), new(JewelRainActionKind.Drop, 4)),
            (At(21, 2, 30), new(JewelRainActionKind.End, 0)),
        };
        Assert.That(timeline, Is.EqualTo(expected));
    }

    /// <summary>
    /// Starting the server inside the countdown skips the marks which are already past.
    /// </summary>
    [Test]
    public void RestartInsideCountdownSkipsPassedMarks()
    {
        var timeline = Run(JewelRainConfiguration.Default, At(20, 57, 30), At(20, 59, 30));

        Assert.That(timeline.Select(t => t.Action), Is.EqualTo(new JewelRainAction[]
        {
            new(JewelRainActionKind.Countdown, 2),
            new(JewelRainActionKind.Countdown, 1),
        }));
    }

    /// <summary>
    /// A start which is noticed far too late is skipped instead of started late.
    /// </summary>
    [Test]
    public void LateStartIsMissed()
    {
        var timeline = Run(JewelRainConfiguration.Default, At(21, 0, 10), At(21, 4, 0));

        Assert.That(timeline.Select(t => t.Action), Is.EqualTo(new JewelRainAction[] { new(JewelRainActionKind.Missed, 0) }));
    }

    /// <summary>
    /// A countdown can begin on the previous day.
    /// </summary>
    [Test]
    public void CountdownCrossesMidnight()
    {
        var config = JewelRainConfiguration.Default;
        config.Days = [DayOfWeek.Thursday];
        config.StartTimes = [new TimeOnly(0, 2)];

        var timeline = Run(config, At(23, 56, 0), Wednesday.AddDays(1).AddMinutes(1));

        Assert.That(timeline.First(), Is.EqualTo((At(23, 57, 0), new JewelRainAction(JewelRainActionKind.Countdown, 5))));
    }

    /// <summary>
    /// A second start time inside a running event does not start another run.
    /// </summary>
    [Test]
    public void OverlappingStartIsSkipped()
    {
        var config = JewelRainConfiguration.Default;
        config.StartTimes = [new TimeOnly(21, 0), new TimeOnly(21, 1)];

        var timeline = Run(config, At(20, 54, 0), At(21, 10, 0));

        Assert.That(timeline.Count(t => t.Action.Kind == JewelRainActionKind.Start), Is.EqualTo(1));
        Assert.That(timeline.Count(t => t.Action.Kind == JewelRainActionKind.End), Is.EqualTo(1));
    }

    /// <summary>
    /// Nothing happens without steps, when disabled, or on another day.
    /// </summary>
    /// <param name="scenario">The scenario.</param>
    [TestCase("empty")]
    [TestCase("disabled")]
    [TestCase("wrongday")]
    public void NothingHappens(string scenario)
    {
        var config = JewelRainConfiguration.Default;
        var from = At(20, 54, 0);
        switch (scenario)
        {
            case "empty":
                config.Steps = [];
                break;
            case "disabled":
                config.Enabled = false;
                break;
            default:
                from = from.AddDays(-1); // Tuesday
                break;
        }

        var timeline = Run(config, from, from.AddMinutes(10));

        Assert.That(timeline, Is.Empty);
    }

    /// <summary>
    /// Editing the configuration during a run does not change that run.
    /// </summary>
    [Test]
    public void ConfigEditDuringRunKeepsSnapshot()
    {
        var config = JewelRainConfiguration.Default;
        var scheduler = new JewelRainScheduler();
        var timeline = new List<(DateTime Time, JewelRainAction Action)>();
        for (var t = At(20, 59, 55); t <= At(21, 3, 0); t = t.AddSeconds(1))
        {
            foreach (var action in scheduler.Advance(config, t))
            {
                timeline.Add((t, action));
            }

            if (t == At(21, 0, 1))
            {
                config.StepInterval = TimeSpan.FromSeconds(5);
            }
        }

        var drops = timeline.Where(x => x.Action.Kind == JewelRainActionKind.Drop).Select(x => x.Time).ToList();
        Assert.That(drops, Is.EqualTo(new[] { At(21, 0, 0), At(21, 0, 30), At(21, 1, 0), At(21, 1, 30), At(21, 2, 0) }));
    }

    /// <summary>
    /// A forced start runs immediately without a countdown.
    /// </summary>
    [Test]
    public void ForceStartRunsImmediately()
    {
        var scheduler = new JewelRainScheduler();
        scheduler.RequestForceStart();

        var actions = scheduler.Advance(JewelRainConfiguration.Default, At(12, 0, 0));

        Assert.That(actions, Is.EqualTo(new JewelRainAction[]
        {
            new(JewelRainActionKind.Start, 0),
            new(JewelRainActionKind.Drop, 0),
        }));
    }

    private static DateTime At(int hour, int minute, int second) => Wednesday.AddHours(hour).AddMinutes(minute).AddSeconds(second);

    private static List<(DateTime Time, JewelRainAction Action)> Run(JewelRainConfiguration config, DateTime from, DateTime to)
    {
        var scheduler = new JewelRainScheduler();
        var result = new List<(DateTime, JewelRainAction)>();
        for (var t = from; t <= to; t = t.AddSeconds(1))
        {
            foreach (var action in scheduler.Advance(config, t))
            {
                result.Add((t, action));
            }
        }

        return result;
    }
}
