// <copyright file="AuctionJewelIdentities.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// The loose jewel of each jewel currency mode, by item group and number. The values match the Auction House currency catalog.
/// </summary>
public static class AuctionJewelIdentities
{
    /// <summary>
    /// Gets the loose jewel of a currency mode.
    /// </summary>
    /// <param name="currency">The currency mode as its wire value.</param>
    /// <returns>The group and number, or <c>null</c> for Zen and Fruits.</returns>
    public static (byte Group, short Number)? GetLooseJewel(byte currency)
        => currency switch
        {
            1 => (12, 15),
            2 => (14, 13),
            3 => (14, 14),
            4 => (14, 16),
            5 => (14, 22),
            6 => (14, 31),
            7 => (14, 42),
            _ => null,
        };
}
