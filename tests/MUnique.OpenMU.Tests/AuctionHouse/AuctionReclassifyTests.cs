// <copyright file="AuctionReclassifyTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Moq;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.Model;

/// <summary>
/// Verifies the one-off re-classification of listings created before sub-categories existed.
/// </summary>
[TestFixture]
public class AuctionReclassifyTests
{
    /// <summary>
    /// Verifies the service hands the batch size to the repository and returns its count.
    /// </summary>
    [Test]
    public async Task ReclassifyDelegatesToTheRepository()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        repository.Setup(r => r.ReclassifyListingsAsync(25, It.IsAny<CancellationToken>())).ReturnsAsync(7);
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = true });

        var changed = await service.ReclassifyListingsAsync(25);

        Assert.That(changed, Is.EqualTo(7));
        repository.Verify(r => r.ReclassifyListingsAsync(25, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies the repository re-classifies unclassified listings from their stored item identity, a batch at a time, and
    /// that a finished backlog stays untouched on the next run. Requires <c>AUCTION_TEST_CONNECTION_STRING</c>.
    /// </summary>
    [Test]
    [Category("PostgreSql")]
    public async Task ReclassifyRunsInBatchesUntilNothingIsLeft()
    {
        if (AuctionPostgresTestEnvironment.ConnectionString is null)
        {
            Assert.Ignore($"Set {AuctionPostgresTestEnvironment.ConnectionStringVariable} to run the PostgreSQL integration tests.");
        }

        AuctionPostgresTestEnvironment.EnsureConfigured();
        var seller = Guid.NewGuid();
        try
        {
            await using (var context = new EntityDataContext())
            {
                foreach (var (group, number) in new[] { (10, 3), (14, 3), (13, 8) })
                {
                    context.Set<AuctionListing>().Add(NewListing(seller, group, number));
                }

                await context.SaveChangesAsync();
            }

            var repository = new AuctionHouseRepository();
            var first = await repository.ReclassifyListingsAsync(new EntityDataContext(), batchSize: 2);
            var second = await repository.ReclassifyListingsAsync(new EntityDataContext(), batchSize: 2);
            var third = await repository.ReclassifyListingsAsync(new EntityDataContext(), batchSize: 2);

            await using var check = new EntityDataContext();
            var rows = await check.Set<AuctionListing>().Where(l => l.SellerAccountId == seller).OrderBy(l => l.ItemGroup).ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(2));
                Assert.That(second, Is.EqualTo(1));
                Assert.That(third, Is.Zero);
                Assert.That(rows.Select(r => ((int)r.Category, (int)r.SubCategory)), Is.EqualTo(new[] { (1, 3), (3, 2), (5, 0) }));
            });
        }
        finally
        {
            await using var cleanup = new EntityDataContext();
            await cleanup.Set<AuctionListing>().Where(l => l.SellerAccountId == seller).ExecuteDeleteAsync();
        }
    }

    private static AuctionListing NewListing(Guid seller, int group, int number)
    {
        var now = DateTimeOffset.UtcNow;
        return new AuctionListing
        {
            Id = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            SellerAccountId = seller,
            SellerCharacterId = Guid.NewGuid(),
            SellerName = "Seller",
            Status = 0,
            Currency = 0,
            Category = 6,
            SubCategory = 255,
            StartingPrice = 1,
            CreatedAt = now,
            OriginalEndsAt = now.AddHours(24),
            EndsAt = now.AddHours(24),
            NormalizedName = string.Empty,
            ItemGroup = group,
            ItemNumber = number,
        };
    }
}
