// <copyright file="SaleFee.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// What the flat sale fee is paid in.
/// </summary>
public enum SaleFeeCurrency
{
    /// <summary>
    /// Zen, the character's money.
    /// </summary>
    Zen = 0,

    /// <summary>
    /// WCoin, an account-level balance players earn by donating or voting. Reserved: the server does not charge it yet.
    /// </summary>
    WCoin = 1,
}

/// <summary>
/// Where a listing's sale fee stands.
/// </summary>
public enum SaleFeeState
{
    /// <summary>
    /// No fee was charged for the listing.
    /// </summary>
    None = 0,

    /// <summary>
    /// The fee was taken when the listing was created and is held until the listing ends.
    /// </summary>
    Held = 1,

    /// <summary>
    /// The listing sold and the fee was kept.
    /// </summary>
    Consumed = 2,

    /// <summary>
    /// The listing ended unsold and the fee was returned to the seller.
    /// </summary>
    Refunded = 3,
}

/// <summary>
/// The flat fee charged once per completed sale, paid by the seller. It replaces the percentage listing and success
/// fees: it is held when a listing is created and returned if the item does not sell.
/// </summary>
/// <param name="Currency">What the fee is paid in.</param>
/// <param name="Amount">The amount in whole units; zero means no fee.</param>
public readonly record struct SaleFee(SaleFeeCurrency Currency, long Amount)
{
    /// <summary>
    /// Gets the approved default: 100,000 Zen.
    /// </summary>
    public static SaleFee Default { get; } = new(SaleFeeCurrency.Zen, 100_000);

    /// <summary>
    /// Gets a value indicating whether the fee is usable: any amount from zero up.
    /// </summary>
    public bool IsValid => this.Amount >= 0;
}
