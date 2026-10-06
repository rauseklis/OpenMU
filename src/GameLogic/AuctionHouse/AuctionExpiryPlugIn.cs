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
public class AuctionExpiryPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<AuctionHouseConfiguration>, ISupportDefaultCustomConfiguration
{
    private DateTime _nextRunUtc = DateTime.MinValue;

    /// <inheritdoc />
    public AuctionHouseConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new AuctionHouseConfiguration();

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var configuration = this.Configuration ??= new AuctionHouseConfiguration();
        AuctionHouseConfigurationSource.Current = configuration;
        var now = DateTime.UtcNow;
        if (!configuration.Enabled || now < this._nextRunUtc)
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
            if (configuration.NotificationsEnabled)
            {
                await DeliverNotificationsAsync(gameContext, service, configuration).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            gameContext.LoggerFactory.CreateLogger<AuctionExpiryPlugIn>().LogError(exception, "Auction House expiry batch failed; it is retried on the next run.");
        }
    }

    private static async Task DeliverNotificationsAsync(GameContext gameContext, AuctionHouseService service, AuctionHouseConfiguration configuration)
    {
        var online = new Dictionary<Guid, Player>();
        await gameContext.ForEachPlayerAsync(player =>
        {
            if (player.Account is MUnique.OpenMU.Persistence.IIdentifiable account)
            {
                online[account.Id] = player;
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
