// <copyright file="PriceComponent.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// One part of a price: a whole quantity of one kind. A component is never converted into another kind, because no
/// exchange rate between kinds exists.
/// </summary>
/// <param name="Kind">The kind the quantity is of.</param>
/// <param name="Quantity">The quantity, in whole units.</param>
public readonly record struct PriceComponent(PriceKind Kind, long Quantity);
