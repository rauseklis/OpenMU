// <copyright file="AuctionNotificationWorkerTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Threading;
using Moq;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Verifies notification delivery continues in maintenance mode, and a failing notification is counted without stopping
/// the rest of the batch.
/// </summary>
[TestFixture]
public class AuctionNotificationWorkerTests
{
    /// <summary>
    /// Verifies maintenance mode continues delivering outcomes created by existing auctions.
    /// </summary>
    [Test]
    public async Task DisabledFeatureStillDeliversNotifications()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = new AuctionNotificationRow(Guid.NewGuid(), Guid.NewGuid(), null, 0, null, null, now);
        var repository = new Mock<IAuctionHouseRepository>(MockBehavior.Strict);
        repository.Setup(r => r.ClaimUndeliveredNotificationsAsync(10, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { notification });
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = false }, () => now);

        var result = await service.DeliverNotificationsAsync(10, _ => Task.FromResult(AuctionNotificationDeliveryOutcome.Delivered));

        Assert.That(result.Delivered, Is.EqualTo(1));
        repository.Verify(r => r.ClaimUndeliveredNotificationsAsync(10, now, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies each claimed notification is delivered, found offline, or counted as failed, and a failure does not stop the batch.
    /// </summary>
    [Test]
    public async Task OneFailureDoesNotStopTheBatch()
    {
        var now = DateTimeOffset.UtcNow;
        var rows = new List<AuctionNotificationRow>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), null, 0, null, null, now),
            new(Guid.NewGuid(), Guid.NewGuid(), null, 1, null, null, now),
            new(Guid.NewGuid(), Guid.NewGuid(), null, 2, null, null, now),
        };
        var repository = new Mock<IAuctionHouseRepository>(MockBehavior.Strict);
        repository.Setup(r => r.ClaimUndeliveredNotificationsAsync(10, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = true });

        var result = await service.DeliverNotificationsAsync(10, async notification =>
        {
            if (notification.Id == rows[1].Id)
            {
                throw new InvalidOperationException("the player's client rejected the message");
            }

            return notification.Id == rows[0].Id ? AuctionNotificationDeliveryOutcome.Delivered : AuctionNotificationDeliveryOutcome.NotOnline;
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Delivered, Is.EqualTo(1));
            Assert.That(result.Failed, Is.EqualTo(1));
            Assert.That(result.NotOnline, Is.EqualTo(1));
        });
    }
}
