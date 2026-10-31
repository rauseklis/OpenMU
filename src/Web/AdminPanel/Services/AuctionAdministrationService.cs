// <copyright file="AuctionAdministrationService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Operator actions on the Auction House: checking the invariants, repairing missing return collections, browsing listings, and
/// removing a listing administratively. Every action runs in the host's persistence, and the repository is absent when the host
/// has no Auction House database.
/// </summary>
public sealed class AuctionAdministrationService
{
    private readonly IPersistenceContextProvider _contextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionAdministrationService"/> class.
    /// </summary>
    /// <param name="contextProvider">The persistence provider of the host.</param>
    public AuctionAdministrationService(IPersistenceContextProvider contextProvider)
    {
        this._contextProvider = contextProvider;
    }

    /// <summary>
    /// Gets a value indicating whether the Auction House database is available in this process.
    /// </summary>
    public bool IsAvailable => this._contextProvider.CreateAuctionHouseRepository() is not null;

    /// <summary>
    /// Checks the stored data against the invariants.
    /// </summary>
    /// <returns>The broken invariants, or <c>null</c> when the database is not available.</returns>
    public async Task<IReadOnlyList<AuctionReconciliationFinding>?> ReconcileAsync()
    {
        if (this.CreateService() is not { } service)
        {
            return null;
        }

        var findings = (await service.ReconcileAsync()).ToList();
        if (this._contextProvider.CreateJewelBankRepository() is { } jewelBank)
        {
            findings.AddRange((await jewelBank.ReconcileAsync()).Select(message => new AuctionReconciliationFinding("JewelBankBalance", message)));
        }

        return findings;
    }

    /// <summary>
    /// Repairs a batch of ended listings which have no return collection.
    /// </summary>
    /// <returns>The repaired and skipped counts, or <c>null</c> when the database is not available.</returns>
    public async Task<AuctionRepairResult?> RepairAsync()
    {
        return this.CreateService() is not { } service ? null : await service.RepairAsync(100);
    }

    /// <summary>
    /// Browses the active listings of one currency.
    /// </summary>
    /// <param name="currency">The currency mode as its wire value.</param>
    /// <returns>The first page of listings, or <c>null</c> when the database is not available.</returns>
    public async Task<AuctionListingPage?> BrowseAsync(byte currency)
    {
        var repository = this._contextProvider.CreateAuctionHouseRepository();
        return repository is null ? null : await repository.SearchListingsAsync(new AuctionListingSearch(currency, PageSize: 50));
    }

    /// <summary>
    /// Removes an active listing administratively.
    /// </summary>
    /// <param name="listingId">The listing.</param>
    /// <param name="administratorIdentity">The administrator's name, recorded in the audit row.</param>
    /// <param name="reason">The reason, recorded in the audit row.</param>
    /// <returns>The outcome, or <c>null</c> when the database is not available.</returns>
    public async Task<AdminRemoveOutcome?> RemoveAsync(Guid listingId, string administratorIdentity, string reason)
    {
        return this.CreateService() is not { } service ? null : await service.AdminRemoveAsync(listingId, administratorIdentity, reason);
    }

    private AuctionHouseService? CreateService()
    {
        var repository = this._contextProvider.CreateAuctionHouseRepository();
        return repository is null ? null : new AuctionHouseService(repository, new AuctionHouseOptions());
    }
}
