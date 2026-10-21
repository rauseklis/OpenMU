// <copyright file="JewelRainConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.JewelRain;

using System.ComponentModel.DataAnnotations;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of the Jewel Rain event.
/// </summary>
public class JewelRainConfiguration
{
    /// <summary>
    /// Gets the default configuration: Wednesday, Friday and Sunday at 21:00 in Devias,
    /// raining 15 of each jewel every 30 seconds.
    /// </summary>
    public static JewelRainConfiguration Default => new()
    {
        Enabled = true,
        Days = [DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Sunday],
        StartTimes = [new TimeOnly(21, 0)],
        CountdownMinutes = [5, 4, 3, 2, 1],
        MapNumber = 2,
        AreaX1 = 0,
        AreaY1 = 0,
        AreaX2 = 255,
        AreaY2 = 255,
        StepInterval = TimeSpan.FromSeconds(30),
        EndDelay = TimeSpan.FromSeconds(30),
        Steps =
        [
            new JewelRainStep(14, 15, 0, 15), // Jewel of Chaos
            new JewelRainStep(14, 14, 0, 15), // Jewel of Soul
            new JewelRainStep(14, 13, 0, 15), // Jewel of Bless
            new JewelRainStep(14, 22, 0, 15), // Jewel of Creation
            new JewelRainStep(14, 16, 0, 15), // Jewel of Life
        ],
    };

    /// <summary>
    /// Gets or sets a value indicating whether the event is enabled.
    /// </summary>
    [Display(Name = "Enabled", Order = 1)]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the days of the week on which the event takes place (server time zone).
    /// </summary>
    [Display(Name = "Days of Week", Order = 2)]
    public IList<DayOfWeek> Days { get; set; } = new List<DayOfWeek>();

    /// <summary>
    /// Gets or sets the start times of the day (server time zone).
    /// </summary>
    [Display(Name = "Start Times", Order = 3)]
    public IList<TimeOnly> StartTimes { get; set; } = new List<TimeOnly>();

    /// <summary>
    /// Gets or sets the minutes before the start at which a countdown message is shown.
    /// </summary>
    [Display(Name = "Countdown Minutes", Order = 4)]
    public IList<int> CountdownMinutes { get; set; } = new List<int>();

    /// <summary>
    /// Gets or sets the number of the map on which the items rain.
    /// </summary>
    [Display(Name = "Map Number", Order = 5)]
    public ushort MapNumber { get; set; }

    /// <summary>
    /// Gets or sets the left border of the drop area.
    /// </summary>
    [Display(Name = "Area X1", Order = 6)]
    public byte AreaX1 { get; set; }

    /// <summary>
    /// Gets or sets the top border of the drop area.
    /// </summary>
    [Display(Name = "Area Y1", Order = 7)]
    public byte AreaY1 { get; set; }

    /// <summary>
    /// Gets or sets the right border of the drop area.
    /// </summary>
    [Display(Name = "Area X2", Order = 8)]
    public byte AreaX2 { get; set; }

    /// <summary>
    /// Gets or sets the bottom border of the drop area.
    /// </summary>
    [Display(Name = "Area Y2", Order = 9)]
    public byte AreaY2 { get; set; }

    /// <summary>
    /// Gets or sets the time between two steps.
    /// </summary>
    [Display(Name = "Step Interval", Order = 10)]
    public TimeSpan StepInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the time between the last drop and the end announcement.
    /// </summary>
    [Display(Name = "End Announcement Delay", Order = 11)]
    public TimeSpan EndDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the ordered steps of the event.
    /// </summary>
    [Display(Name = "Steps", Order = 12)]
    public IList<JewelRainStep> Steps { get; set; } = new List<JewelRainStep>();

    /// <summary>
    /// Gets or sets the countdown message. <c>{minutes}</c> is replaced by the remaining minutes.
    /// </summary>
    [Display(Name = "Countdown Message", Order = 13)]
    public LocalizedString CountdownMessage { get; set; } = "Jewel Rain starts in {minutes} minute(s) in Devias!";

    /// <summary>
    /// Gets or sets the message shown when the event starts.
    /// </summary>
    [Display(Name = "Start Message", Order = 14)]
    public LocalizedString StartMessage { get; set; } = "Jewel Rain has begun in Devias! Run for the jewels!";

    /// <summary>
    /// Gets or sets the message shown after the last drop.
    /// </summary>
    [Display(Name = "End Message", Order = 15)]
    public LocalizedString EndMessage { get; set; } = "Jewel Rain has ended - no more drops.";
}
