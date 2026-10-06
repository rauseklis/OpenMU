// <copyright file="JewelPackRules.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The approved packed-jewel mapping. Only these levels are tender; an item is never treated as a jewel pack by its name.
/// </summary>
public static class JewelPackRules
{
    /// <summary>
    /// Gets the loose-jewel value of a packed jewel level.
    /// </summary>
    /// <param name="level">The packed item level.</param>
    /// <returns>The number of loose units the pack represents.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The level is not part of the approved mapping.</exception>
    public static int GetPackUnits(int level) => level switch
    {
        0 => 10,
        1 => 20,
        2 => 30,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Not an approved packed-jewel level."),
    };
}
