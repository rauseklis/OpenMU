// <copyright file="BankJewelPaymentPlanner.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// How a jewel price is paid from the Jewel Bank: which loose jewels and which bundles are used, and how many loose jewels come back as change.
/// </summary>
/// <param name="LooseUsed">The loose jewels taken.</param>
/// <param name="PacksUsed">The bundles taken, by size: index 0 holds bundles of 10, index 1 of 20 and index 2 of 30.</param>
/// <param name="ChangeUnits">The loose jewels which are credited back because a bundle was larger than the rest of the price.</param>
public sealed record BankJewelPayment(long LooseUsed, IReadOnlyList<long> PacksUsed, long ChangeUnits);

/// <summary>
/// Plans the payment of a jewel price from the Jewel Bank balances. The bank keeps loose jewels and bundles of 10, 20 and 30 as separate counts.
/// </summary>
public static class BankJewelPaymentPlanner
{
    private static readonly int[] PackSizes = [10, 20, 30];

    /// <summary>
    /// Plans a payment: loose jewels first, then bundles from the smallest size up, taking as few whole bundles as cover the rest.
    /// </summary>
    /// <param name="requiredUnits">The price in jewels.</param>
    /// <param name="loose">The loose jewels held.</param>
    /// <param name="packs">The bundles held, by size (10, 20, 30).</param>
    /// <returns>The plan, or <c>null</c> when the bank does not hold enough.</returns>
    public static BankJewelPayment? Plan(long requiredUnits, long loose, IReadOnlyList<long> packs)
    {
        if (requiredUnits <= 0 || packs.Count != PackSizes.Length)
        {
            return null;
        }

        var looseUsed = Math.Min(Math.Max(loose, 0), requiredUnits);
        var remaining = requiredUnits - looseUsed;
        var used = new long[PackSizes.Length];
        for (var index = 0; index < PackSizes.Length && remaining > 0; index++)
        {
            var size = PackSizes[index];
            var wanted = (remaining + size - 1) / size;
            used[index] = Math.Min(Math.Max(packs[index], 0), wanted);
            remaining -= used[index] * size;
        }

        return remaining > 0 ? null : new BankJewelPayment(looseUsed, used, -remaining);
    }
}
