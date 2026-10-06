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
    /// Gets a value indicating whether the Auction House is enabled. It stays disabled until the owner turns it on.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the number of listings per browse page.
    /// </summary>
    public int PageSize { get; init; } = 8;
}
