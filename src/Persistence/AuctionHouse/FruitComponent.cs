// <copyright file="FruitComponent.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// One of the five stat fruits a Fruit Basket can be priced in.
/// </summary>
public enum FruitComponent
{
    /// <summary>
    /// Strength Fruit.
    /// </summary>
    Strength,

    /// <summary>
    /// Agility Fruit.
    /// </summary>
    Agility,

    /// <summary>
    /// Vitality Fruit.
    /// </summary>
    Vitality,

    /// <summary>
    /// Energy Fruit.
    /// </summary>
    Energy,

    /// <summary>
    /// Command Fruit. Server attributes call this Leadership.
    /// </summary>
    Command,
}
