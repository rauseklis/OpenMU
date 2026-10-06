// <copyright file="ListingEligibility.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Where an item currently is. Only items in the main inventory can be listed.
/// </summary>
public enum ItemLocation
{
    /// <summary>
    /// The character's main inventory.
    /// </summary>
    MainInventory,

    /// <summary>
    /// Equipped on the character.
    /// </summary>
    Equipped,

    /// <summary>
    /// Offered in a trade.
    /// </summary>
    Trade,

    /// <summary>
    /// Placed in a personal store.
    /// </summary>
    PersonalStore,

    /// <summary>
    /// In the warehouse.
    /// </summary>
    Warehouse,

    /// <summary>
    /// In the chaos machine or another crafting container.
    /// </summary>
    ChaosMachine,

    /// <summary>
    /// In any other temporary container.
    /// </summary>
    TemporaryContainer,
}

/// <summary>
/// The facts about an item that decide whether it can be listed.
/// </summary>
/// <param name="IsTradable">Whether the item definition is tradable.</param>
/// <param name="IsPersonalStoreSellable">Whether the item definition may be sold in a personal store.</param>
/// <param name="IsBoundToCharacter">Whether the item is bound to its character.</param>
/// <param name="IsQuestItem">Whether the item is a quest item.</param>
/// <param name="Durability">The current durability.</param>
/// <param name="Location">Where the item currently is.</param>
/// <param name="IsAlreadyReferenced">Whether an active listing or pending collection already references the item.</param>
public readonly record struct ListingCandidate(
    bool IsTradable,
    bool IsPersonalStoreSellable,
    bool IsBoundToCharacter,
    bool IsQuestItem,
    int Durability,
    ItemLocation Location,
    bool IsAlreadyReferenced);

/// <summary>
/// The outcome of checking an item for listing. The first failing condition is reported.
/// </summary>
public enum ListingEligibilityResult
{
    /// <summary>
    /// The item can be listed.
    /// </summary>
    Eligible,

    /// <summary>
    /// The item is not in the main inventory.
    /// </summary>
    WrongLocation,

    /// <summary>
    /// The item definition is not tradable.
    /// </summary>
    NotTradable,

    /// <summary>
    /// The item definition may not be sold in a personal store.
    /// </summary>
    NotSellable,

    /// <summary>
    /// The item is bound to its character.
    /// </summary>
    Bound,

    /// <summary>
    /// The item is a quest item.
    /// </summary>
    QuestItem,

    /// <summary>
    /// The item has no durability left.
    /// </summary>
    ZeroDurability,

    /// <summary>
    /// The item is already referenced by an active listing or a pending collection.
    /// </summary>
    AlreadyReferenced,
}

/// <summary>
/// Decides whether an item can be listed, using a fixed order of checks.
/// </summary>
public static class ListingEligibility
{
    /// <summary>
    /// Evaluates an item for listing.
    /// </summary>
    /// <param name="candidate">The item facts.</param>
    /// <returns>The first failing condition, or <see cref="ListingEligibilityResult.Eligible"/>.</returns>
    public static ListingEligibilityResult Evaluate(ListingCandidate candidate)
    {
        if (candidate.Location != ItemLocation.MainInventory)
        {
            return ListingEligibilityResult.WrongLocation;
        }

        if (!candidate.IsTradable)
        {
            return ListingEligibilityResult.NotTradable;
        }

        if (!candidate.IsPersonalStoreSellable)
        {
            return ListingEligibilityResult.NotSellable;
        }

        if (candidate.IsBoundToCharacter)
        {
            return ListingEligibilityResult.Bound;
        }

        if (candidate.IsQuestItem)
        {
            return ListingEligibilityResult.QuestItem;
        }

        if (candidate.Durability <= 0)
        {
            return ListingEligibilityResult.ZeroDurability;
        }

        return candidate.IsAlreadyReferenced ? ListingEligibilityResult.AlreadyReferenced : ListingEligibilityResult.Eligible;
    }
}
