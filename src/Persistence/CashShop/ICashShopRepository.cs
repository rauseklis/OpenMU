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
}

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
