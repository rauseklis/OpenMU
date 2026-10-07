// <copyright file="AuctionHouseOptions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The Auction House settings which the service reads on each request.
/// </summary>
public sealed record AuctionHouseOptions
{
    /// <summary>
    /// Gets a value indicating whether new player auction mutations are enabled. It stays disabled until the owner
    /// turns it on; recovery and maintenance operations do not depend on it.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the number of listings per browse page.
    /// </summary>
    public int PageSize { get; init; } = 24;

    /// <summary>
    /// Gets the listing fee in basis points of the starting price. The approved fee is 1%, rounded up with a minimum of one unit.
    /// </summary>
    public int ListingFeeBasisPoints { get; init; } = 100;

    /// <summary>
    /// Gets the success fee in basis points of the final sale price. The approved fee is 5%, rounded up with a minimum of one unit.
    /// </summary>
    public int SuccessFeeBasisPoints { get; init; } = 500;

    /// <summary>
    /// Gets the maximum number of active listings per account.
    /// </summary>
    public int MaximumActiveListingsPerAccount { get; init; } = 10;

    /// <summary>
    /// Gets the maximum number of active listings per character.
    /// </summary>
    public int MaximumActiveListingsPerCharacter { get; init; } = 5;
}
