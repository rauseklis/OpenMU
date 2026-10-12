// <copyright file="SaleFeePolicy.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The lifecycle of the flat sale fee: a charged fee is held from the moment the listing is created, is kept when the
/// item sells, and is returned for every other ending (cancelled, expired unsold, removed by an administrator).
/// </summary>
public static class SaleFeePolicy
{
    /// <summary>
    /// Gets the state a new listing's fee starts in.
    /// </summary>
    /// <param name="fee">The fee charged for the listing.</param>
    /// <returns><see cref="SaleFeeState.Held"/> when a fee is charged, otherwise <see cref="SaleFeeState.None"/>.</returns>
    public static SaleFeeState InitialState(SaleFee fee) => fee.Amount > 0 ? SaleFeeState.Held : SaleFeeState.None;

    /// <summary>
    /// Gets the state of a fee after the listing reached a status. Only a held fee changes.
    /// </summary>
    /// <param name="current">The fee state before.</param>
    /// <param name="status">The status of the listing.</param>
    /// <returns>The fee state after.</returns>
    public static SaleFeeState StateAfter(SaleFeeState current, ListingStatus status)
    {
        if (current != SaleFeeState.Held)
        {
            return current;
        }

        return status switch
        {
            ListingStatus.Active => SaleFeeState.Held,
            ListingStatus.Sold => SaleFeeState.Consumed,
            _ => SaleFeeState.Refunded,
        };
    }

    /// <summary>
    /// Determines whether a fee in this state has to be returned to the seller, through the Mailbox.
    /// </summary>
    /// <param name="state">The fee state.</param>
    /// <returns><c>true</c> when a refund is owed.</returns>
    public static bool IsRefundOwed(SaleFeeState state) => state == SaleFeeState.Refunded;
}
