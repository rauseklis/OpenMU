// <copyright file="PriceKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// One of the things a price can be paid in. A listing's price is a list of components, each of one kind, so a price
/// can mix Zen, jewels and stat fruits. The values are the wire values and must not change; the first eight match the
/// scalar <see cref="AuctionCurrencyMode"/> values.
/// </summary>
public enum PriceKind
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
    /// Fruit of Strength.
    /// </summary>
    StrengthFruit = 8,

    /// <summary>
    /// Fruit of Agility.
    /// </summary>
    AgilityFruit = 9,

    /// <summary>
    /// Fruit of Vitality.
    /// </summary>
    VitalityFruit = 10,

    /// <summary>
    /// Fruit of Energy.
    /// </summary>
    EnergyFruit = 11,

    /// <summary>
    /// Fruit of Command. Server attributes call this Leadership.
    /// </summary>
    CommandFruit = 12,
}
