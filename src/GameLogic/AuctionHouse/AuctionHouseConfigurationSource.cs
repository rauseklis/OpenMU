// <copyright file="AuctionHouseConfigurationSource.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The Auction House configuration which is in effect. The expiry worker publishes the configuration it loaded, so the
/// packet handlers use the same settings. Until the worker runs, the feature is disabled.
/// </summary>
public static class AuctionHouseConfigurationSource
{
    /// <summary>
    /// Gets or sets the configuration in effect.
    /// </summary>
    public static AuctionHouseConfiguration Current { get; set; } = new();
}
