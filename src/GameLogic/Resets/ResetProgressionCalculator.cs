// <copyright file="ResetProgressionCalculator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

/// <summary>
/// Calculates costs and rewards for the next character reset.
/// </summary>
public static class ResetProgressionCalculator
{
    /// <summary>
    /// Calculates the reset progression for the next reset.
    /// </summary>
    /// <param name="currentResetCount">The current reset count.</param>
    /// <param name="pointsPerResetOverride">The player-specific points per reset override (0 means not configured).</param>
    /// <param name="configuration">The reset configuration.</param>
    /// <returns>The calculated progression.</returns>
    public static ResetProgression Calculate(int currentResetCount, int pointsPerResetOverride, ResetConfiguration configuration)
    {
        var nextResetCount = currentResetCount + 1;
        var requiredZen = GetRequiredZen(configuration, nextResetCount);

        var pointsForReset = GetPointsForReset(configuration, pointsPerResetOverride, nextResetCount);
        var totalPointsAfterReset = GetTotalPointsAfterReset(configuration, pointsPerResetOverride, nextResetCount, pointsForReset);
        var requiredItemAmount = GetRequiredItemAmount(configuration, nextResetCount);

        return new ResetProgression(nextResetCount, requiredZen, requiredItemAmount, pointsForReset, totalPointsAfterReset);
    }

    private static int GetRequiredZen(ResetConfiguration configuration, int nextResetCount)
    {
        long requiredZen;
        if (GetMatchingTier(configuration.MoneyCostTiers, nextResetCount, tier => tier.MinimumResetCount) is { } tier)
        {
            requiredZen = Math.Max(0, tier.RequiredMoney);
            if (tier.MultiplyByResetCount)
            {
                requiredZen *= nextResetCount;
            }
        }
        else
        {
            requiredZen = Math.Max(0, configuration.RequiredMoney);
            if (configuration.MultiplyRequiredMoneyByResetCount)
            {
                requiredZen *= nextResetCount;
            }
        }

        return (int)Math.Min(requiredZen, int.MaxValue);
    }

    private static int GetPointsForReset(ResetConfiguration configuration, int pointsPerResetOverride, int nextResetCount)
    {
        if (GetMatchingTier(configuration.PointsTiers, nextResetCount, tier => tier.MinimumResetCount) is { } tier)
        {
            return Math.Max(0, tier.PointsGranted);
        }

        var pointsPerReset = pointsPerResetOverride == 0 ? configuration.PointsPerReset : pointsPerResetOverride;
        if (configuration.MultiplyPointsByResetCount)
        {
            pointsPerReset *= nextResetCount;
        }

        return Math.Max(0, pointsPerReset);
    }

    private static int GetRequiredItemAmount(ResetConfiguration configuration, int nextResetCount)
    {
        if (configuration.RequiredResetItem is null && !configuration.PayItemCostsWithJewelBankFruits)
        {
            return 0;
        }

        if (GetMatchingTier(configuration.ItemCostTiers, nextResetCount, tier => tier.MinimumResetCount) is not { } tier)
        {
            return 0;
        }

        var amount = (long)tier.RequiredItemAmount + ((long)tier.AdditionalItemAmountPerReset * (nextResetCount - tier.MinimumResetCount));
        return (int)Math.Clamp(amount, 0, int.MaxValue);
    }

    private static int GetTotalPointsAfterReset(ResetConfiguration configuration, int pointsPerResetOverride, int nextResetCount, int pointsForReset)
    {
        if (configuration.PointsTiers.Count == 0)
        {
            return pointsForReset;
        }

        long total = 0;
        for (var resetCount = 1; resetCount <= nextResetCount; resetCount++)
        {
            total += GetPointsForReset(configuration, pointsPerResetOverride, resetCount);
            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return (int)total;
    }

    private static TTier? GetMatchingTier<TTier>(IEnumerable<TTier> tiers, int resetCount, Func<TTier, int> getMinimumResetCount)
        where TTier : class
    {
        return tiers
            .OrderByDescending(getMinimumResetCount)
            .FirstOrDefault(tier => getMinimumResetCount(tier) <= resetCount);
    }
}
