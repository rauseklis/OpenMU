// <copyright file="AuctionHouseConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The Auction House settings. They are stored in the plug-in configuration of the expiry worker. The feature stays
/// disabled until the owner sets <see cref="Enabled"/>.
/// </summary>
public class AuctionHouseConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the Auction House is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the number of listings per browse page.
    /// </summary>
    public int PageSize { get; set; } = 8;

    /// <summary>
    /// Gets or sets the listing fee in basis points of the starting price.
    /// </summary>
    public int ListingFeeBasisPoints { get; set; } = 100;

    /// <summary>
    /// Gets or sets the success fee in basis points of the final sale price.
    /// </summary>
    public int SuccessFeeBasisPoints { get; set; } = 500;

    /// <summary>
    /// Gets or sets the maximum number of active listings per account.
    /// </summary>
    public int MaximumActiveListingsPerAccount { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of active listings per character.
    /// </summary>
    public int MaximumActiveListingsPerCharacter { get; set; } = 5;

    /// <summary>
    /// Gets or sets the number of listings the expiry worker finalizes in one batch.
    /// </summary>
    public int ExpiryBatchSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the number of seconds between two expiry batches.
    /// </summary>
    public int ExpiryIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Creates the service options from this configuration.
    /// </summary>
    /// <returns>The options.</returns>
    public AuctionHouseOptions ToOptions()
    {
        return new AuctionHouseOptions
        {
            Enabled = this.Enabled,
            PageSize = this.PageSize,
            ListingFeeBasisPoints = this.ListingFeeBasisPoints,
            SuccessFeeBasisPoints = this.SuccessFeeBasisPoints,
            MaximumActiveListingsPerAccount = this.MaximumActiveListingsPerAccount,
            MaximumActiveListingsPerCharacter = this.MaximumActiveListingsPerCharacter,
        };
    }
}
