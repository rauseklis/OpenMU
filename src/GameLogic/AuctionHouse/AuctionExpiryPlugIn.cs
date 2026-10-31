// <copyright file="AuctionExpiryPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.IO;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Finalizes Auction House listings whose expiry has passed. Several game servers can run it at once: the batch locks its
/// listings with <c>SKIP LOCKED</c>, so no listing is finalized twice.
/// </summary>
[PlugIn]
[Guid("44B287BD-F981-4918-95F7-27AAF5D69998")]
public class AuctionExpiryPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<AuctionHouseConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly TimeSpan IniCheckInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan JewelBankMaintenanceInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan JewelBankOperationRetention = TimeSpan.FromDays(30);
    private const int JewelBankPurgeBatchSize = 500;

    /// <summary>
    /// The environment variable which moves <c>AuctionHouse.ini</c> to another place, for example a volume of a container.
    /// </summary>
    private const string IniPathVariable = "AUCTION_HOUSE_INI_PATH";

    private readonly AuctionHouseIniFile _iniFile = new(Environment.GetEnvironmentVariable(IniPathVariable) is { Length: > 0 } iniPath ? iniPath : Path.Combine(AppContext.BaseDirectory, "AuctionHouse.ini"));
    private DateTime _nextRunUtc = DateTime.MinValue;
    private DateTime _nextIniCheckUtc = DateTime.MinValue;
    private DateTime _nextJewelBankMaintenanceUtc = DateTime.MinValue;

    /// <inheritdoc />
    public AuctionHouseConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new AuctionHouseConfiguration();

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var configuration = this.Configuration ??= new AuctionHouseConfiguration();
        var now = DateTime.UtcNow;
        this.RefreshIniFile(gameContext, configuration, now);
        AuctionHouseConfigurationSource.Current = configuration;
        // Enabled is the kill switch for new player mutations. Existing listings, notifications, operation cleanup,
        // and collections must continue to progress in maintenance mode so no item or escrow can be stranded.
        if (now < this._nextRunUtc)
        {
            return;
        }

        this._nextRunUtc = now + TimeSpan.FromSeconds(configuration.ExpiryIntervalSeconds);
        var repository = gameContext.PersistenceContextProvider.CreateAuctionHouseRepository();
        if (repository is null)
        {
            return;
        }

        try
        {
            var service = new AuctionHouseService(repository, configuration.ToOptions());
            await service.FinalizeDueListingsAsync(configuration.ExpiryBatchSize).ConfigureAwait(false);
            await service.ReclassifyListingsAsync(configuration.ExpiryBatchSize).ConfigureAwait(false);
            if (configuration.NotificationsEnabled)
            {
                await DeliverNotificationsAsync(gameContext, service, configuration).ConfigureAwait(false);
            }

            await service.PurgeOperationsAsync(TimeSpan.FromDays(configuration.OperationRetentionDays), configuration.PurgeBatchSize).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            gameContext.LoggerFactory.CreateLogger<AuctionExpiryPlugIn>().LogError(exception, "Auction House expiry batch failed; it is retried on the next run.");
        }

        await this.RunJewelBankMaintenanceAsync(gameContext, now).ConfigureAwait(false);
    }

    /// <summary>
    /// Once an hour, checks the Jewel Bank balances against their movements (a problem is logged as a warning) and deletes old
    /// request records, so that neither a mistake in the data nor a growing table goes unnoticed.
    /// </summary>
    private async ValueTask RunJewelBankMaintenanceAsync(GameContext gameContext, DateTime now)
    {
        if (now < this._nextJewelBankMaintenanceUtc)
        {
            return;
        }

        this._nextJewelBankMaintenanceUtc = now + JewelBankMaintenanceInterval;
        var logger = gameContext.LoggerFactory.CreateLogger<AuctionExpiryPlugIn>();
        try
        {
            if (gameContext.PersistenceContextProvider.CreateJewelBankRepository() is not { } repository)
            {
                return;
            }

            var service = new JewelBank.JewelBankService(repository, null);
            foreach (var finding in await service.ReconcileAsync().ConfigureAwait(false))
            {
                logger.LogWarning("Jewel Bank check: {Finding}", finding);
            }

            int purged;
            do
            {
                purged = await service.PurgeOperationsAsync(JewelBankOperationRetention, JewelBankPurgeBatchSize).ConfigureAwait(false);
            }
            while (purged >= JewelBankPurgeBatchSize);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The Jewel Bank maintenance failed; it is retried in an hour.");
        }
    }

    /// <summary>
    /// Reads the hand-editable <c>AuctionHouse.ini</c> next to the server whenever it changed, so the owner can adjust
    /// settings without the admin panel or a restart. The file is created from the current settings on first use.
    /// </summary>
    private void RefreshIniFile(GameContext gameContext, AuctionHouseConfiguration configuration, DateTime now)
    {
        if (now < this._nextIniCheckUtc)
        {
            return;
        }

        this._nextIniCheckUtc = now + IniCheckInterval;
        var result = this._iniFile.RefreshIfChanged(configuration);
        if (result is null)
        {
            return;
        }

        var logger = gameContext.LoggerFactory.CreateLogger<AuctionExpiryPlugIn>();
        logger.LogInformation("Auction House settings read from {Path}: {Count} applied.", this._iniFile.Path, result.AppliedCount);
        foreach (var error in result.Errors)
        {
            logger.LogWarning("AuctionHouse.ini: {Error}", error);
        }
    }

    private static async Task DeliverNotificationsAsync(GameContext gameContext, AuctionHouseService service, AuctionHouseConfiguration configuration)
    {
        var online = new Dictionary<Guid, Player>();
        await gameContext.ForEachPlayerAsync(player =>
        {
            if (player.GetServerScopedAccountId() is { } accountId)
            {
                online[accountId] = player;
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);

        await service.DeliverNotificationsAsync(configuration.NotificationBatchSize, async notification =>
        {
            if (!online.TryGetValue(notification.AccountId, out var player))
            {
                return AuctionNotificationDeliveryOutcome.NotOnline;
            }

            await player.ShowLocalizedBlueMessageAsync(MessageKeyOf(notification.Kind)).AsTask().ConfigureAwait(false);
            return AuctionNotificationDeliveryOutcome.Delivered;
        }).ConfigureAwait(false);
    }

    private static string MessageKeyOf(byte kind)
        => kind switch
        {
            0 => "AuctionNotificationOutbid",
            1 => "AuctionNotificationSold",
            2 => "AuctionNotificationBought",
            3 => "AuctionNotificationExpired",
            _ => "AuctionNotificationReturned",
        };

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextRunUtc = DateTime.MinValue;
    }
}
