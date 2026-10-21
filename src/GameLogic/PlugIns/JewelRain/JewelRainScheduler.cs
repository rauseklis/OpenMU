// <copyright file="JewelRainScheduler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.JewelRain;

/// <summary>
/// The kind of an action which the <see cref="JewelRainScheduler"/> asks to execute.
/// </summary>
public enum JewelRainActionKind
{
    /// <summary>
    /// A countdown message; the value is the remaining minutes.
    /// </summary>
    Countdown,

    /// <summary>
    /// The event starts.
    /// </summary>
    Start,

    /// <summary>
    /// A step drops; the value is the zero-based step index.
    /// </summary>
    Drop,

    /// <summary>
    /// The end announcement.
    /// </summary>
    End,

    /// <summary>
    /// A scheduled start was noticed too late and is skipped.
    /// </summary>
    Missed,
}

/// <summary>
/// An action which should be executed by the Jewel Rain plug-in.
/// </summary>
/// <param name="Kind">The kind of the action.</param>
/// <param name="Value">The minutes for a countdown, the step index for a drop, otherwise 0.</param>
public readonly record struct JewelRainAction(JewelRainActionKind Kind, int Value);

/// <summary>
/// A pure, clock-driven state machine which turns the current server time into
/// <see cref="JewelRainAction"/>s. It knows nothing about players, maps or items.
/// </summary>
public sealed class JewelRainScheduler
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan LookBack = TimeSpan.FromMinutes(1);

    private DateTime _lastHandledStart = DateTime.MinValue;

    private bool _forceStart;

    private Run? _run;

    /// <summary>
    /// Gets a value indicating whether a run is currently armed or in progress.
    /// </summary>
    public bool IsRunning => this._run is not null;

    /// <summary>
    /// Requests that the next <see cref="Advance"/> starts a run immediately, without countdown.
    /// </summary>
    public void RequestForceStart() => this._forceStart = true;

    /// <summary>
    /// Advances the state machine to the given time.
    /// </summary>
    /// <param name="config">The current configuration.</param>
    /// <param name="nowLocal">The current wall clock time in the server time zone.</param>
    /// <returns>The actions to execute, in order.</returns>
    public IReadOnlyList<JewelRainAction> Advance(JewelRainConfiguration config, DateTime nowLocal)
    {
        var actions = new List<JewelRainAction>();
        if (this._run is null)
        {
            this._run = this.TryArm(config, nowLocal);
        }

        if (this._run is { } run)
        {
            if (this.Process(run, nowLocal, actions))
            {
                this._run = null;
            }
        }

        return actions;
    }

    private Run? TryArm(JewelRainConfiguration config, DateTime nowLocal)
    {
        if (!config.Enabled || config.Steps.Count == 0)
        {
            this._forceStart = false;
            return null;
        }

        if (this._forceStart)
        {
            this._forceStart = false;
            return new Run(nowLocal, config, skipCountdown: true);
        }

        var start = FindNextStart(config, nowLocal, this._lastHandledStart);
        if (start is null)
        {
            return null;
        }

        var maxCountdown = config.CountdownMinutes.Count == 0 ? 0 : config.CountdownMinutes.Max();
        return nowLocal >= start.Value.AddMinutes(-maxCountdown)
            ? new Run(start.Value, config, skipCountdown: false)
            : null;
    }

    private static DateTime? FindNextStart(JewelRainConfiguration config, DateTime nowLocal, DateTime lastHandled)
    {
        var earliest = nowLocal - LookBack;
        DateTime? best = null;
        for (var offset = 0; offset <= 8; offset++)
        {
            var day = earliest.Date.AddDays(offset);
            if (!config.Days.Contains(day.DayOfWeek))
            {
                continue;
            }

            foreach (var time in config.StartTimes)
            {
                var candidate = day + time.ToTimeSpan();
                if (candidate > lastHandled && candidate > earliest && (best is null || candidate < best))
                {
                    best = candidate;
                }
            }
        }

        return best;
    }

    private bool Process(Run run, DateTime nowLocal, List<JewelRainAction> actions)
    {
        foreach (var minutes in run.PendingMarks.OrderByDescending(m => m).ToList())
        {
            var due = run.Start.AddMinutes(-minutes);
            if (nowLocal < due)
            {
                continue;
            }

            run.PendingMarks.Remove(minutes);
            if (nowLocal - due <= Tolerance)
            {
                actions.Add(new JewelRainAction(JewelRainActionKind.Countdown, minutes));
            }
        }

        if (!run.Started)
        {
            if (nowLocal < run.Start)
            {
                return false;
            }

            this._lastHandledStart = run.Start;
            if (nowLocal - run.Start > Tolerance)
            {
                actions.Add(new JewelRainAction(JewelRainActionKind.Missed, 0));
                return true;
            }

            run.Started = true;
            actions.Add(new JewelRainAction(JewelRainActionKind.Start, 0));
        }

        while (run.NextStep < run.StepCount && nowLocal >= run.Start + (run.NextStep * run.Interval))
        {
            actions.Add(new JewelRainAction(JewelRainActionKind.Drop, run.NextStep));
            run.NextStep++;
        }

        var endTime = run.Start + ((run.StepCount - 1) * run.Interval) + run.EndDelay;
        if (run.NextStep >= run.StepCount && nowLocal >= endTime)
        {
            actions.Add(new JewelRainAction(JewelRainActionKind.End, 0));
            return true;
        }

        return false;
    }

    private sealed class Run
    {
        public Run(DateTime start, JewelRainConfiguration config, bool skipCountdown)
        {
            this.Start = start;
            this.StepCount = config.Steps.Count;
            this.Interval = config.StepInterval;
            this.EndDelay = config.EndDelay;
            this.PendingMarks = skipCountdown ? new List<int>() : config.CountdownMinutes.Where(m => m > 0).Distinct().ToList();
        }

        public DateTime Start { get; }

        public int StepCount { get; }

        public TimeSpan Interval { get; }

        public TimeSpan EndDelay { get; }

        public List<int> PendingMarks { get; }

        public bool Started { get; set; }

        public int NextStep { get; set; }
    }
}
