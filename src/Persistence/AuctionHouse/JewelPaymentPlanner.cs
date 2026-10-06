// <copyright file="JewelPaymentPlanner.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A jewel item available for payment: its inventory slot and the loose units it is worth (1 for a loose jewel,
/// 10, 20 or 30 for a pack).
/// </summary>
/// <param name="Slot">The inventory slot.</param>
/// <param name="Units">The loose units the item is worth.</param>
public readonly record struct JewelStack(int Slot, int Units);

/// <summary>
/// The jewels chosen to pay an amount, and the change owed back as a collection.
/// </summary>
/// <param name="Slots">The chosen inventory slots, in ascending order.</param>
/// <param name="ChangeUnits">The overpayment in loose units, to be issued as change.</param>
public readonly record struct JewelPayment(IReadOnlyList<int> Slots, long ChangeUnits);

/// <summary>
/// Chooses which jewels pay an amount. The choice is deterministic: least overpayment first, then fewest items, then
/// the lowest slots. Change is returned as a value, so it is never silently discarded.
/// </summary>
public static class JewelPaymentPlanner
{
    private const int LooseUnits = 1;

    /// <summary>
    /// Plans the payment for an amount from the candidate jewels.
    /// </summary>
    /// <param name="requiredUnits">The amount to pay, in loose units.</param>
    /// <param name="candidates">The eligible jewels, with the item being listed already excluded.</param>
    /// <returns>The payment, or <c>null</c> if the candidates cannot cover the amount.</returns>
    public static JewelPayment? Plan(long requiredUnits, IEnumerable<JewelStack> candidates)
    {
        var loose = SlotsWorth(candidates, LooseUnits);
        var tens = SlotsWorth(candidates, JewelPackRules.GetPackUnits(0));
        var twenties = SlotsWorth(candidates, JewelPackRules.GetPackUnits(1));
        var thirties = SlotsWorth(candidates, JewelPackRules.GetPackUnits(2));

        JewelPayment? best = null;
        long bestOverpayment = long.MaxValue;
        var bestCount = int.MaxValue;
        List<int>? bestSlots = null;

        for (var l = 0; l <= loose.Count; l++)
        {
            for (var t = 0; t <= tens.Count; t++)
            {
                for (var w = 0; w <= twenties.Count; w++)
                {
                    for (var h = 0; h <= thirties.Count; h++)
                    {
                        var total = (long)l + (10L * t) + (20L * w) + (30L * h);
                        if (total < requiredUnits)
                        {
                            continue;
                        }

                        var overpayment = total - requiredUnits;
                        var count = l + t + w + h;
                        if (overpayment > bestOverpayment || (overpayment == bestOverpayment && count > bestCount))
                        {
                            continue;
                        }

                        var slots = loose.Take(l).Concat(tens.Take(t)).Concat(twenties.Take(w)).Concat(thirties.Take(h))
                            .OrderBy(slot => slot)
                            .ToList();
                        if (overpayment < bestOverpayment || count < bestCount || IsLowerSlotOrder(slots, bestSlots))
                        {
                            bestOverpayment = overpayment;
                            bestCount = count;
                            bestSlots = slots;
                            best = new JewelPayment(slots, overpayment);
                        }
                    }
                }
            }
        }

        return best;
    }

    private static List<int> SlotsWorth(IEnumerable<JewelStack> candidates, int units)
    {
        return candidates.Where(stack => stack.Units == units).Select(stack => stack.Slot).OrderBy(slot => slot).ToList();
    }

    private static bool IsLowerSlotOrder(IReadOnlyList<int> slots, IReadOnlyList<int>? other)
    {
        if (other is null)
        {
            return true;
        }

        for (var i = 0; i < Math.Min(slots.Count, other.Count); i++)
        {
            if (slots[i] != other[i])
            {
                return slots[i] < other[i];
            }
        }

        return slots.Count < other.Count;
    }
}
