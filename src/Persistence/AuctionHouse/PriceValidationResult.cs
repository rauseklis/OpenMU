// <copyright file="PriceValidationResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The outcome of checking a listing's prices.
/// </summary>
public enum PriceValidationResult
{
    /// <summary>
    /// The prices are valid.
    /// </summary>
    Valid,

    /// <summary>
    /// The listing has neither a starting price nor a buyout price.
    /// </summary>
    NoPrice,

    /// <summary>
    /// A price has more components than the configured maximum.
    /// </summary>
    TooManyComponents,

    /// <summary>
    /// A price names the same kind twice.
    /// </summary>
    DuplicateComponent,

    /// <summary>
    /// A component's quantity is outside the range of its kind.
    /// </summary>
    QuantityOutOfRange,

    /// <summary>
    /// A starting price has more than one component. Only a single-component price takes bids.
    /// </summary>
    MixedStartingPrice,

    /// <summary>
    /// An auction's buyout does not use exactly the kind of its starting price.
    /// </summary>
    BuyoutKindsDiffer,

    /// <summary>
    /// An auction's buyout is below its starting price.
    /// </summary>
    BuyoutBelowStart,
}
