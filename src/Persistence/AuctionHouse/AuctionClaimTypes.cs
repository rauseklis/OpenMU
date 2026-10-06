// <copyright file="AuctionClaimTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

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

    /// <summary>
    /// The requested quantity is zero, exceeds what remains, or a Zen collection was requested in part.
    /// </summary>
    InvalidClaimQuantity,

    /// <summary>
    /// The character has no free main-inventory slot for the returned item.
    /// </summary>
    InventoryFull,
}

/// <summary>
/// A quantity of each stat fruit, used for fruit-basket claims.
/// </summary>
/// <param name="Strength">The Strength Fruit quantity.</param>
/// <param name="Agility">The Agility Fruit quantity.</param>
/// <param name="Vitality">The Vitality Fruit quantity.</param>
/// <param name="Energy">The Energy Fruit quantity.</param>
/// <param name="Command">The Command Fruit quantity.</param>
public readonly record struct FruitQuantities(long Strength, long Agility, long Vitality, long Energy, long Command);

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
/// <param name="RequestedScalar">The scalar quantity to claim, for jewel collections; null claims everything that remains.</param>
/// <param name="RequestedFruits">The fruit quantities to claim, for fruit-basket collections; null claims everything that remains.</param>
public readonly record struct ClaimDraft(
    Guid OperationId,
    Guid AccountId,
    Guid CharacterId,
    Guid CollectionId,
    int ExpectedVersion,
    DateTimeOffset Now,
    long? RequestedScalar = null,
    FruitQuantities? RequestedFruits = null);
