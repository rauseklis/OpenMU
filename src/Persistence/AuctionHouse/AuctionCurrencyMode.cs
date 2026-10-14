// <copyright file="AuctionCurrencyMode.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The single currency mode a listing is priced in. The values match the wire catalog in the design.
/// </summary>
public enum AuctionCurrencyMode
{
    /// <summary>
    /// Zen, the character's money.
    /// </summary>
    Zen = 0,

    /// <summary>
    /// Jewel of Chaos.
    /// </summary>
    Chaos = 1,

    /// <summary>
    /// Jewel of Bless.
    /// </summary>
    Bless = 2,

    /// <summary>
    /// Jewel of Soul.
    /// </summary>
    Soul = 3,

    /// <summary>
    /// Jewel of Life.
    /// </summary>
    Life = 4,

    /// <summary>
    /// Jewel of Creation.
    /// </summary>
    Creation = 5,

    /// <summary>
    /// Jewel of Guardian.
    /// </summary>
    Guardian = 6,

    /// <summary>
    /// Jewel of Harmony.
    /// </summary>
    Harmony = 7,

    /// <summary>
    /// A seller-defined basket of the five stat fruits. Priced per component, never as a scalar.
    /// </summary>
    Fruits = 8,

    /// <summary>
    /// A price made of two or more kinds (Zen, jewels, stat fruits) in any mix, or a buy-now price. Stored as a basket of 13 quantities.
    /// </summary>
    Mixed = 9,
}
