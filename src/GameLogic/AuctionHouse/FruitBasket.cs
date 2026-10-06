// <copyright file="FruitBasket.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A quantity of each stat fruit. A basket is a vector: it is never collapsed into a total, because no exchange
/// rate between fruit types exists.
/// </summary>
/// <param name="Strength">The Strength Fruit quantity.</param>
/// <param name="Agility">The Agility Fruit quantity.</param>
/// <param name="Vitality">The Vitality Fruit quantity.</param>
/// <param name="Energy">The Energy Fruit quantity.</param>
/// <param name="Command">The Command Fruit quantity.</param>
public readonly record struct FruitBasket(long Strength, long Agility, long Vitality, long Energy, long Command)
{
    /// <summary>
    /// Gets the quantity of one component.
    /// </summary>
    /// <param name="component">The fruit component.</param>
    /// <returns>The quantity of that fruit.</returns>
    public long Get(FruitComponent component) => component switch
    {
        FruitComponent.Strength => this.Strength,
        FruitComponent.Agility => this.Agility,
        FruitComponent.Vitality => this.Vitality,
        FruitComponent.Energy => this.Energy,
        _ => this.Command,
    };
}
