// <copyright file="AuctionEligibility.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The requirements a character and its account must meet before a mutation. Browsing has no such requirement.
/// </summary>
public static class AuctionEligibility
{
    /// <summary>
    /// Evaluates a mutation requester. The first failing requirement is reported: restriction, then level, then account age.
    /// </summary>
    /// <param name="level">The character level.</param>
    /// <param name="registeredUtc">When the account was registered, in UTC.</param>
    /// <param name="nowUtc">The current time, in UTC.</param>
    /// <param name="minimumLevel">The minimum character level.</param>
    /// <param name="minimumAgeHours">The minimum account age in hours.</param>
    /// <param name="restricted">Whether the account has an active Auction House restriction.</param>
    /// <returns>The refusal, or <c>null</c> when the requester is eligible.</returns>
    public static AuctionResultCode? Evaluate(int level, DateTime registeredUtc, DateTime nowUtc, int minimumLevel, int minimumAgeHours, bool restricted)
    {
        if (restricted)
        {
            return AuctionResultCode.AccountRestricted;
        }

        if (level < minimumLevel)
        {
            return AuctionResultCode.LevelTooLow;
        }

        if (nowUtc - registeredUtc < TimeSpan.FromHours(minimumAgeHours))
        {
            return AuctionResultCode.AccountTooNew;
        }

        return null;
    }
}
