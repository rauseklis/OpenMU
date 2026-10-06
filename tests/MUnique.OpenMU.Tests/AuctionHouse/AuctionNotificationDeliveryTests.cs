// <copyright file="AuctionNotificationDeliveryTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Microsoft.EntityFrameworkCore;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies notification delivery against PostgreSQL: a notification is claimed once and marked delivered, so a second claim
/// does not return it.
/// Requires <c>AUCTION_TEST_CONNECTION_STRING</c>; otherwise the tests are skipped.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class AuctionNotificationDeliveryTests
{
    private Guid _notificationId;
    private Guid _accountId;

    [OneTimeSetUp]
    public void SetUp()
    {
        if (AuctionPostgresTestEnvironment.ConnectionString is null)
        {
            Assert.Ignore($"Set {AuctionPostgresTestEnvironment.ConnectionStringVariable} to run the PostgreSQL integration tests.");
        }

        AuctionPostgresTestEnvironment.EnsureConfigured();
    }

    [SetUp]
    public async Task Arrange()
    {
        this._accountId = Guid.NewGuid();
        this._notificationId = Guid.NewGuid();
        await using var context = new EntityDataContext();
        context.Set<AuctionNotification>().Add(new AuctionNotification
        {
            Id = this._notificationId,
            AccountId = this._accountId,
            Kind = 4,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task Cleanup()
    {
        await using var context = new EntityDataContext();
        await context.Set<AuctionNotification>().Where(n => n.AccountId == this._accountId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Verifies an undelivered notification is claimed once: the second claim does not return it.
    /// </summary>
    [Test]
    public async Task NotificationIsClaimedOnce()
    {
        var repository = new AuctionHouseRepository();

        var first = await repository.ClaimUndeliveredNotificationsAsync(1000, DateTimeOffset.UtcNow);
        var second = await repository.ClaimUndeliveredNotificationsAsync(1000, DateTimeOffset.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(first.Any(n => n.Id == this._notificationId), Is.True);
            Assert.That(second.Any(n => n.Id == this._notificationId), Is.False);
        });
    }
}
