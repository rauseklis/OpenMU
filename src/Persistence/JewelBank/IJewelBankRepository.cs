// <copyright file="IJewelBankRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.JewelBank;

using System.Threading;

/// <summary>
/// The outcome of a Jewel Bank operation, as its wire value.
/// </summary>
public enum JewelBankResultCode : byte
{
    /// <summary>The operation was applied.</summary>
    Success = 0,

    /// <summary>The request was malformed (unknown kind, non-positive or too large amount).</summary>
    InvalidRequest = 1,

    /// <summary>The bank holds fewer units than were asked for.</summary>
    InsufficientBalance = 2,

    /// <summary>The items do not fit into the free inventory space.</summary>
    InventoryFull = 3,

    /// <summary>The per-kind limit is reached; nothing was stored.</summary>
    CapReached = 4,

    /// <summary>There is nothing in the inventory which the bank accepts.</summary>
    NothingToDeposit = 5,

    /// <summary>The server's item configuration lacks a required item definition.</summary>
    NotConfigured = 6,

    /// <summary>The player's current state does not permit the operation (for example outside a safe zone).</summary>
    StateRestricted = 7,

    /// <summary>The request was sent too fast.</summary>
    RateLimited = 8,

    /// <summary>The Jewel Bank is not available on this server.</summary>
    ServiceUnavailable = 9,
}

/// <summary>
/// The stored counts of an account.
/// </summary>
/// <param name="Counts">The count of each kind, by kind code. Kinds without a row are zero.</param>
/// <param name="AutoDeposit">Whether picked-up jewels and fruits are sent to the bank.</param>
public sealed record JewelBankState(IReadOnlyDictionary<byte, long> Counts, bool AutoDeposit)
{
    /// <summary>
    /// Gets the count of a kind.
    /// </summary>
    /// <param name="code">The kind code.</param>
    /// <returns>The count, zero when nothing is stored.</returns>
    public long CountOf(byte code) => this.Counts.TryGetValue(code, out var count) ? count : 0;
}

/// <summary>
/// The result of a deposit or withdrawal.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="Moved">The units which moved, by kind code (deposited units for a deposit, withdrawn units for a withdrawal).</param>
/// <param name="State">The state after the operation, or the unchanged state on refusal; <c>null</c> when it could not be read.</param>
public sealed record JewelBankOperationResult(JewelBankResultCode Result, IReadOnlyDictionary<byte, long> Moved, JewelBankState? State)
{
    /// <summary>
    /// Gets a value indicating whether the operation was applied.
    /// </summary>
    public bool IsSuccess => this.Result == JewelBankResultCode.Success;

    /// <summary>
    /// Creates a refusal.
    /// </summary>
    /// <param name="result">The refusal.</param>
    /// <returns>The result with no movement.</returns>
    public static JewelBankOperationResult Refused(JewelBankResultCode result)
        => new(result, new Dictionary<byte, long>(), null);
}

/// <summary>
/// Stores the Jewel Bank. Each mutation is one database transaction, and its operation record is written with it,
/// so an operation either happens fully or not at all, and a repeated operation identifier applies nothing twice.
/// </summary>
public interface IJewelBankRepository
{
    /// <summary>
    /// Reads the stored counts and the auto-deposit choice of an account.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The state; empty counts and auto-deposit off for an account which never used the bank.</returns>
    Task<JewelBankState> GetStateAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the bankable items of a character's inventory into the bank. Items which are not plain (options, sockets) stay.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="characterId">The character whose inventory is emptied of bankable items.</param>
    /// <param name="operationId">The client-made identifier which makes a repeated request harmless.</param>
    /// <param name="requested">The units to deposit, by kind code; a kind which is not listed, or listed with 0, is left alone. <c>null</c> deposits everything the bank accepts.</param>
    /// <param name="maximumPerKind">The most units the bank may hold of one kind.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<JewelBankOperationResult> DepositFromInventoryAsync(Guid accountId, Guid characterId, Guid operationId, IReadOnlyDictionary<byte, long>? requested, long maximumPerKind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes units out of the bank and creates them as items in a character's inventory, packs first.
    /// Either everything is placed or nothing changes.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="characterId">The character who receives the items.</param>
    /// <param name="operationId">The client-made identifier which makes a repeated request harmless.</param>
    /// <param name="units">The units to withdraw, by kind code; entries of 0 are ignored.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<JewelBankOperationResult> WithdrawToInventoryAsync(Guid accountId, Guid characterId, Guid operationId, IReadOnlyDictionary<byte, long> units, CancellationToken cancellationToken = default);

    /// <summary>
    /// Credits units which have no item rows, because they were picked up from the floor.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="characterId">The character who picked the item up.</param>
    /// <param name="kindCode">The kind.</param>
    /// <param name="units">The amount of units, positive.</param>
    /// <param name="maximumPerKind">The most units the bank may hold of one kind.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new count of the kind when credited; <c>null</c> when the limit would be exceeded or the request is invalid.</returns>
    Task<long?> CreditPickupAsync(Guid accountId, Guid characterId, byte kindCode, long units, long maximumPerKind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the auto-deposit choice of an account.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="enabled">The choice.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task which completes when the choice is stored.</returns>
    Task SetAutoDepositAsync(Guid accountId, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the invariants of the stored data: balances equal the sum of their movements and are never negative.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A description of each broken invariant; empty when the data is consistent.</returns>
    Task<IReadOnlyList<string>> ReconcileAsync(CancellationToken cancellationToken = default);
}
