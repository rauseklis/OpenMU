// <copyright file="JewelBankLimits.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// The limit of the Jewel Bank which the persistence layer needs, so the Auction House can credit claimed jewels under the same limit as a deposit.
/// The game logic's <c>JewelBankOptions</c> sets it.
/// </summary>
public static class JewelBankLimits
{
    /// <summary>
    /// Gets or sets the most units the bank holds of one kind of jewel or fruit.
    /// </summary>
    public static long MaximumPerKind { get; set; } = 25_000;
}
