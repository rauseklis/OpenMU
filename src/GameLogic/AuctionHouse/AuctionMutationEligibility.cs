// <copyright file="AuctionMutationEligibility.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// A snapshot of the online gameplay state which controls whether an Auction House mutation may start.
/// </summary>
/// <param name="HasEnteredWorld">Whether the character is in the ordinary entered-world state.</param>
/// <param name="IsAlive">Whether the character is alive.</param>
/// <param name="IsInSafeZone">Whether the character is standing in a safe zone.</param>
/// <param name="IsTeleporting">Whether a teleport is in progress.</param>
/// <param name="IsTrading">Whether the character is involved in a trade.</param>
/// <param name="IsVending">Whether the personal store is open.</param>
/// <param name="HasOpenedNpc">Whether an NPC-backed window is open.</param>
/// <param name="IsDueling">Whether the character is fighting or spectating in a duel room.</param>
/// <param name="IsInMiniGame">Whether the character is participating in a mini-game.</param>
/// <param name="IsInGuildWar">Whether the character is participating in a requested or active guild war.</param>
public readonly record struct AuctionMutationState(
    bool HasEnteredWorld,
    bool IsAlive,
    bool IsInSafeZone,
    bool IsTeleporting,
    bool IsTrading,
    bool IsVending,
    bool HasOpenedNpc,
    bool IsDueling,
    bool IsInMiniGame,
    bool IsInGuildWar);

/// <summary>
/// Evaluates the server-authoritative gameplay-state restrictions for create, bid, buyout, cancel, and collect.
/// </summary>
public static class AuctionMutationEligibility
{
    /// <summary>
    /// Determines whether a mutation may start from the supplied state.
    /// </summary>
    /// <param name="state">The current player-state snapshot.</param>
    /// <returns><see langword="true"/> when no gameplay state conflicts with the mutation.</returns>
    public static bool IsAllowed(AuctionMutationState state)
    {
        return state.HasEnteredWorld
               && state.IsAlive
               && state.IsInSafeZone
               && !state.IsTeleporting
               && !state.IsTrading
               && !state.IsVending
               && !state.HasOpenedNpc
               && !state.IsDueling
               && !state.IsInMiniGame
               && !state.IsInGuildWar;
    }
}
