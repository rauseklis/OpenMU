// <copyright file="CrywolfPenaltyItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Crywolf;

/// <summary>
/// Identifies an item whose drop is reduced while the penalties of an occupied crywolf fortress apply.
/// </summary>
public class CrywolfPenaltyItem
{
    /// <summary>
    /// Gets or sets the group of the item.
    /// </summary>
    public byte Group { get; set; }

    /// <summary>
    /// Gets or sets the number of the item in its group.
    /// </summary>
    public short Number { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{this.Group}/{this.Number}";
    }
}
