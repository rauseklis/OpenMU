// <copyright file="JewelRainStep.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.JewelRain;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// One step of the Jewel Rain: a single item type which is dropped in a given amount.
/// </summary>
public class JewelRainStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JewelRainStep"/> class.
    /// Required for serialization and UI binding.
    /// </summary>
    public JewelRainStep()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JewelRainStep"/> class.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number within the group.</param>
    /// <param name="level">The item level.</param>
    /// <param name="amount">The amount of separate ground items to drop.</param>
    public JewelRainStep(byte group, short number, byte level, ushort amount)
    {
        this.Group = group;
        this.Number = number;
        this.Level = level;
        this.Amount = amount;
    }

    /// <summary>
    /// Gets or sets the item group.
    /// </summary>
    [Display(Name = "Item Group", Order = 1)]
    public byte Group { get; set; }

    /// <summary>
    /// Gets or sets the item number within the group.
    /// </summary>
    [Display(Name = "Item Number", Order = 2)]
    public short Number { get; set; }

    /// <summary>
    /// Gets or sets the item level.
    /// </summary>
    [Display(Name = "Item Level", Order = 3)]
    public byte Level { get; set; }

    /// <summary>
    /// Gets or sets the amount of separate ground items which are dropped in this step.
    /// </summary>
    [Display(Name = "Amount", Order = 4)]
    public ushort Amount { get; set; } = 1;
}
