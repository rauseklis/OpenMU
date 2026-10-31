// <copyright file="JewelBankMaintenanceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.JewelBank;

using System.Threading;
using Moq;
using MUnique.OpenMU.GameLogic.JewelBank;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// Verifies the checks and the cleanup which the server runs on the Jewel Bank data, and the limit on the requests which only read it.
/// </summary>
[TestFixture]
public class JewelBankMaintenanceTests
{
    /// <summary>
    /// The cleanup deletes request records older than the retention time, not newer ones.
    /// </summary>
    [Test]
    public async Task PurgeDeletesRecordsOlderThanTheRetentionTime()
    {
        var repository = new Mock<IJewelBankRepository>();
        repository.Setup(r => r.PurgeOperationsAsync(It.IsAny<DateTimeOffset>(), 500, It.IsAny<CancellationToken>())).ReturnsAsync(7);
        var before = DateTimeOffset.UtcNow;

        var purged = await new JewelBankService(repository.Object, null).PurgeOperationsAsync(TimeSpan.FromDays(30), 500);

        Assert.That(purged, Is.EqualTo(7));
        repository.Verify(
            r => r.PurgeOperationsAsync(It.Is<DateTimeOffset>(cutoff => cutoff >= before.AddDays(-30) && cutoff <= DateTimeOffset.UtcNow.AddDays(-30)), 500, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The balance check gives the findings of the repository back unchanged.
    /// </summary>
    [Test]
    public async Task ReconcileReturnsTheFindingsOfTheRepository()
    {
        var repository = new Mock<IJewelBankRepository>();
        repository.Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "Account A, kind 3: balance 5 differs from its movements (4)." });

        var findings = await new JewelBankService(repository.Object, null).ReconcileAsync();

        Assert.That(findings, Is.EqualTo(new[] { "Account A, kind 3: balance 5 differs from its movements (4)." }));
    }

    /// <summary>
    /// The window asks every 1.5 seconds, which the limit on the requests which only read the state must allow, while a client
    /// which asks much faster is held back.
    /// </summary>
    [Test]
    public void TheWindowsOwnPaceIsAllowedButFasterRequestsAreNot()
    {
        Assert.That(JewelBankOptions.MinimumOpenInterval, Is.LessThan(TimeSpan.FromMilliseconds(1500)));

        var now = DateTimeOffset.UtcNow;
        var limiter = new JewelBankRateLimiter(() => now, JewelBankOptions.MinimumOpenInterval);
        var account = Guid.NewGuid();
        Assert.That(limiter.TryAcquire(account), Is.True);
        now = now.AddMilliseconds(100);
        Assert.That(limiter.TryAcquire(account), Is.False);
        now = now.AddMilliseconds(1400);
        Assert.That(limiter.TryAcquire(account), Is.True);
    }
}
