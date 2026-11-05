// <copyright file="AuctionBankWallet.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// What an account can spend in jewels and fruits at the Auction House: its Jewel Bank balances.
/// </summary>
public static class AuctionBankWallet
{
    private static readonly int[] PackSizes = [10, 20, 30];

    /// <summary>
    /// Gets the bank kind code of a jewel currency.
    /// </summary>
    /// <param name="currency">The currency mode, 1 (Chaos) to 7 (Harmony).</param>
    /// <returns>The code, or <c>null</c> when the currency is not a jewel.</returns>
    public static byte? JewelCode(byte currency)
        => AuctionJewelIdentities.GetLooseJewel(currency) is { } id && JewelBankKinds.Find(id.Group, id.Number, 0) is { } kind ? kind.Code : null;

    /// <summary>
    /// Gets the bank kind code of a fruit.
    /// </summary>
    /// <param name="component">The fruit.</param>
    /// <returns>The code.</returns>
    public static byte FruitCode(FruitComponent component) => (byte)(JewelBankKinds.FirstFruitCode + FruitRules.GetItemLevel(component));

    /// <summary>
    /// Gets the code of a bundle kind of a jewel kind.
    /// </summary>
    /// <param name="looseCode">The code of the loose jewel.</param>
    /// <param name="sizeIndex">0 for bundles of 10, 1 for 20, 2 for 30.</param>
    /// <returns>The code.</returns>
    public static byte PackCode(byte looseCode, int sizeIndex) => (byte)(looseCode + JewelBankKinds.JewelCount + 5 + (JewelBankKinds.JewelCount * sizeIndex));

    /// <summary>
    /// Gets what a jewel currency is worth in the bank.
    /// </summary>
    /// <param name="state">The bank state.</param>
    /// <param name="currency">The currency mode, 1 to 7.</param>
    /// <returns>The loose jewels, the bundle counts by size (10, 20, 30) and the total jewels; all zero for an unknown currency.</returns>
    public static (long Loose, long[] Packs, long Total) Jewels(JewelBankState state, byte currency)
    {
        if (JewelCode(currency) is not { } code)
        {
            return (0, new long[PackSizes.Length], 0);
        }

        var loose = state.CountOf(code);
        var packs = new long[PackSizes.Length];
        var total = loose;
        for (var index = 0; index < PackSizes.Length; index++)
        {
            packs[index] = state.CountOf(PackCode(code, index));
            total += packs[index] * PackSizes[index];
        }

        return (loose, packs, total);
    }

    /// <summary>
    /// Gets the fruits of the bank.
    /// </summary>
    /// <param name="state">The bank state.</param>
    /// <returns>The counts of the five fruits.</returns>
    public static FruitQuantities Fruits(JewelBankState state)
        => new(
            state.CountOf(FruitCode(FruitComponent.Strength)),
            state.CountOf(FruitCode(FruitComponent.Agility)),
            state.CountOf(FruitCode(FruitComponent.Vitality)),
            state.CountOf(FruitCode(FruitComponent.Energy)),
            state.CountOf(FruitCode(FruitComponent.Command)));
}
