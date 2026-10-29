// <copyright file="ChaosItemUpgradeRates.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

/// <summary>The approved shared x5/x50 item upgrade rates, independent of other Chaos recipes.</summary>
public static class ChaosItemUpgradeRates
{
    /// <summary>The additive success percentage for an item with Luck.</summary>
    public const int LuckBonus = 20;

    /// <summary>Returns the approved base chance for the target item level.</summary>
    /// <param name="targetLevel">The resulting level, from ten to fifteen.</param>
    /// <returns>The base success percentage before item modifiers.</returns>
    public static byte GetBaseRate(byte targetLevel) => targetLevel switch
    {
        10 or 11 => 60,
        12 or 13 => 55,
        14 or 15 => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(targetLevel)),
    };
}
