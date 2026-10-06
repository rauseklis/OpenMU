// <copyright file="AuctionExpiryPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Finalizes Auction House listings whose expiry has passed. Several game servers can run it at once: the batch locks its
/// listings with <c>SKIP LOCKED</c>, so no listing is finalized twice.
/// </summary>
[PlugIn]
[Guid("44B287BD-F981-4918-95F7-27AAF5D69998")]
public class AuctionExpiryPlugIn : IPeriodicTaskPlugIn
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private DateTime _nextRunUtc = DateTime.MinValue;

    /// <summary>
    /// Gets or sets the Auction House options. The worker does nothing while the feature is disabled.
    /// </summary>
    public AuctionHouseOptions Options { get; set; } = new();

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (!this.Options.Enabled || now < this._nextRunUtc)
        {
            return;
        }

        this._nextRunUtc = now + Interval;
        var repository = gameContext.PersistenceContextProvider.CreateAuctionHouseRepository();
        if (repository is null)
        {
            return;
        }

        try
        {
            var service = new AuctionHouseService(repository, this.Options);
            await service.FinalizeDueListingsAsync(BatchSize).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            gameContext.LoggerFactory.CreateLogger<AuctionExpiryPlugIn>().LogError(exception, "Auction House expiry batch failed; it is retried on the next run.");
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextRunUtc = DateTime.MinValue;
    }
}
