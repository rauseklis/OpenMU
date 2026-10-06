// <copyright file="CurrencyCatalog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// The identity of an item definition: its group, number and level.
/// </summary>
/// <param name="Group">The item group.</param>
/// <param name="Number">The item number within the group.</param>
/// <param name="Level">The item level.</param>
public readonly record struct ItemIdentity(int Group, int Number, int Level);

/// <summary>
/// An inventory item considered as tender. Only the fields that decide eligibility are included.
/// </summary>
/// <param name="Group">The item group.</param>
/// <param name="Number">The item number within the group.</param>
/// <param name="Level">The item level.</param>
/// <param name="Durability">The current durability.</param>
/// <param name="HasOptions">Whether the item has excellent, ancient, harmony, socket or other options.</param>
/// <param name="IsBound">Whether the item is bound to its character.</param>
public readonly record struct TenderCandidate(int Group, int Number, int Level, int Durability, bool HasOptions, bool IsBound);

/// <summary>
/// The canonical currency items for each mode. Eligibility is decided by item identity, never by localized name.
/// </summary>
public static class CurrencyCatalog
{
    private const int FruitGroup = 13;
    private const int FruitNumber = 15;

    /// <summary>
    /// Gets the canonical loose identity of a jewel mode.
    /// </summary>
    /// <param name="mode">The currency mode.</param>
    /// <returns>The identity, or <c>null</c> for Zen and the Fruits basket, which have no single loose item.</returns>
    public static ItemIdentity? GetLooseIdentity(AuctionCurrencyMode mode)
    {
        return mode switch
        {
            AuctionCurrencyMode.Chaos => new ItemIdentity(12, 15, 0),
            AuctionCurrencyMode.Bless => new ItemIdentity(14, 13, 0),
            AuctionCurrencyMode.Soul => new ItemIdentity(14, 14, 0),
            AuctionCurrencyMode.Life => new ItemIdentity(14, 16, 0),
            AuctionCurrencyMode.Creation => new ItemIdentity(14, 22, 0),
            AuctionCurrencyMode.Guardian => new ItemIdentity(14, 31, 0),
            AuctionCurrencyMode.Harmony => new ItemIdentity(14, 42, 0),
            _ => null,
        };
    }

    /// <summary>
    /// Determines whether an item is clean canonical tender for a currency mode. The item must have positive durability,
    /// no options, no binding, and the exact canonical identity for the mode.
    /// </summary>
    /// <param name="mode">The currency mode.</param>
    /// <param name="candidate">The inventory item.</param>
    /// <returns><c>true</c> if the item can pay in that mode.</returns>
    public static bool IsCanonicalTender(AuctionCurrencyMode mode, TenderCandidate candidate)
    {
        if (mode == AuctionCurrencyMode.Zen || candidate.Durability <= 0 || candidate.HasOptions || candidate.IsBound)
        {
            return false;
        }

        if (mode == AuctionCurrencyMode.Fruits)
        {
            return candidate.Group == FruitGroup
                   && candidate.Number == FruitNumber
                   && Enum.GetValues<FruitComponent>().Any(component => FruitRules.GetItemLevel(component) == candidate.Level);
        }

        var identity = GetLooseIdentity(mode);
        return identity is { } expected
               && expected.Group == candidate.Group
               && expected.Number == candidate.Number
               && candidate.Level == expected.Level;
    }
}
