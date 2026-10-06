// <copyright file="AuctionClaimTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

/// <summary>
/// The outcome of a collection claim.
/// </summary>
public enum AuctionClaimOutcome
{
    /// <summary>
    /// The collection was claimed. The result says what to deliver.
    /// </summary>
    Claimed,

    /// <summary>
    /// The operation was already processed; nothing was claimed again.
    /// </summary>
    Replayed,

    /// <summary>
    /// No such collection belongs to the requesting account.
    /// </summary>
    NotFound,

    /// <summary>
    /// The collection changed since the client last saw it.
    /// </summary>
    Stale,

    /// <summary>
    /// The collection was already claimed.
    /// </summary>
    AlreadyClaimed,

    /// <summary>
    /// Crediting the Zen would exceed the inventory money limit, so nothing was claimed.
    /// </summary>
    MoneyLimit,
}

/// <summary>
/// The result of a collection claim, with what the game server must deliver.
/// </summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Kind">The collection kind as its wire value.</param>
/// <param name="ItemId">The item to deliver, for item collections.</param>
/// <param name="Amount">The scalar amount to credit, for scalar collections.</param>
public readonly record struct AuctionClaimResult(AuctionClaimOutcome Outcome, byte Kind, Guid? ItemId, long Amount);

/// <summary>
/// A request to claim a collection.
/// </summary>
/// <param name="OperationId">The client-generated operation identifier.</param>
/// <param name="AccountId">The claiming account.</param>
/// <param name="CharacterId">The claiming character, whose inventory receives Zen.</param>
/// <param name="CollectionId">The collection to claim.</param>
/// <param name="ExpectedVersion">The collection version the client last saw.</param>
/// <param name="Now">The time of the claim, in UTC.</param>
public readonly record struct ClaimDraft(Guid OperationId, Guid AccountId, Guid CharacterId, Guid CollectionId, int ExpectedVersion, DateTimeOffset Now);
