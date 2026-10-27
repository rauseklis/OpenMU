// <copyright file="ICashShopRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.CashShop;

using System.Threading;

/// <summary>Persistent Cash Shop balances, isolated by account and game server.</summary>
public interface ICashShopRepository
{
    /// <summary>Reads balances; a missing wallet has zero of every currency.</summary>
    /// <param name="accountId">The real account identifier.</param>
    /// <param name="serverId">The game server identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The committed balances.</returns>
    Task<CashShopBalance> GetBalanceAsync(Guid accountId, byte serverId, CancellationToken cancellationToken = default);

    /// <summary>Applies an administrative credit exactly once. No player packet exposes this method.</summary>
    /// <param name="accountId">The real account identifier.</param>
    /// <param name="serverId">The game server identifier.</param>
    /// <param name="operationId">The retry-safe administrative operation identifier.</param>
    /// <param name="credit">The integer amounts to add.</param>
    /// <param name="reason">The administrative audit reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The balances after the original operation.</returns>
    Task<CashShopBalance> CreditAsync(Guid accountId, byte serverId, Guid operationId, CashShopBalance credit, string reason, CancellationToken cancellationToken = default);

    /// <summary>Atomically debits the price and stores the bought item in the account's Cash Shop storage.</summary>
    /// <param name="accountId">The real account identifier.</param>
    /// <param name="serverId">The game server identifier.</param>
    /// <param name="currency">The currency which pays the price.</param>
    /// <param name="price">The price, in whole units of the currency.</param>
    /// <param name="item">The item to store; its <see cref="CashShopStorageItem.Id"/> is ignored.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome; the balance is unchanged unless it is <see cref="CashShopPurchaseResult.Success"/>.</returns>
    Task<CashShopPurchaseResult> PurchaseAsync(Guid accountId, byte serverId, CashShopCurrency currency, long price, CashShopStorageItem item, CancellationToken cancellationToken = default);

    /// <summary>Lists the storage items of the account, oldest first.</summary>
    /// <param name="accountId">The real account identifier.</param>
    /// <param name="serverId">The game server identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All stored items.</returns>
    Task<IReadOnlyList<CashShopStorageItem>> ListStorageAsync(Guid accountId, byte serverId, CancellationToken cancellationToken = default);

    /// <summary>Removes one storage item and returns it; null when it does not exist (also when another request took it first).</summary>
    /// <param name="accountId">The real account identifier.</param>
    /// <param name="serverId">The game server identifier.</param>
    /// <param name="storageItemId">The storage item identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The removed item, or null.</returns>
    Task<CashShopStorageItem?> TakeStorageItemAsync(Guid accountId, byte serverId, long storageItemId, CancellationToken cancellationToken = default);

    /// <summary>Puts a taken item back (when it could not be delivered), keeping its identifier.</summary>
    /// <param name="accountId">The real account identifier.</param>
    /// <param name="serverId">The game server identifier.</param>
    /// <param name="item">The item which was taken before.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    Task RestoreStorageItemAsync(Guid accountId, byte serverId, CashShopStorageItem item, CancellationToken cancellationToken = default);
}

/// <summary>The currencies of the Cash Shop.</summary>
public enum CashShopCurrency
{
    /// <summary>WCoinC.</summary>
    WCoinC,

    /// <summary>WCoinP.</summary>
    WCoinP,

    /// <summary>Goblin Points.</summary>
    GoblinPoints,
}

/// <summary>The outcome of a purchase.</summary>
public enum CashShopPurchaseResult
{
    /// <summary>The price was debited and the item stored.</summary>
    Success,

    /// <summary>The balance of the currency is too low.</summary>
    InsufficientFunds,

    /// <summary>The storage holds the maximum number of items.</summary>
    StorageFull,
}

/// <summary>An item which waits in the Cash Shop storage until the player uses it.</summary>
/// <param name="Id">The storage item identifier (0 before it is stored).</param>
/// <param name="PackageSeq">The shop script package.</param>
/// <param name="ProductSeq">The shop script product.</param>
/// <param name="PriceSeq">The shop script price option.</param>
/// <param name="ItemGroup">The item group of the delivered item.</param>
/// <param name="ItemNumber">The item number of the delivered item.</param>
/// <param name="ItemLevel">The item level of the delivered item.</param>
/// <param name="Quantity">The number of items (stack size) to deliver.</param>
public sealed record CashShopStorageItem(long Id, int PackageSeq, int ProductSeq, int PriceSeq, byte ItemGroup, short ItemNumber, byte ItemLevel, int Quantity);

/// <summary>Integer currency units; all fields are nonnegative and capped for the existing client.</summary>
/// <param name="WCoinC">WCoinC units.</param>
/// <param name="WCoinP">WCoinP units.</param>
/// <param name="GoblinPoints">Goblin Point units.</param>
public sealed record CashShopBalance(long WCoinC = 0, long WCoinP = 0, long GoblinPoints = 0)
{
    /// <summary>The largest supported balance of one currency.</summary>
    public const long Maximum = 2_000_000_000;

    /// <summary>Checks that a balance or credit fits the persistent and wire limits.</summary>
    /// <returns>Whether every amount is within range.</returns>
    public bool IsValid() => this.WCoinC is >= 0 and <= Maximum
        && this.WCoinP is >= 0 and <= Maximum
        && this.GoblinPoints is >= 0 and <= Maximum;
}
