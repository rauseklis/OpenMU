// <copyright file="AuctionMetricsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Diagnostics.Metrics;
using System.Threading;
using Moq;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Verifies the Auction House instruments: a new listing records its fee once per currency, and a replay records its own outcome
/// without recording the fee or the operation as a new economic event again.
/// </summary>
[TestFixture]
public class AuctionMetricsTests
{
    private readonly List<(string Name, long Value, Dictionary<string, object?> Tags)> _measurements = new();
    private MeterListener? _listener;

    /// <summary>
    /// Starts listening to the Auction House meter.
    /// </summary>
    [SetUp]
    public void StartListening()
    {
        this._measurements.Clear();
        this._listener = new MeterListener();
        this._listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AuctionHouseMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        this._listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            var dictionary = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                dictionary[tag.Key] = tag.Value;
            }

            lock (this._measurements)
            {
                this._measurements.Add((instrument.Name, value, dictionary));
            }
        });
        this._listener.Start();
    }

    /// <summary>
    /// Stops listening.
    /// </summary>
    [TearDown]
    public void StopListening()
    {
        this._listener?.Dispose();
    }

    /// <summary>
    /// Verifies a new Zen listing records its fee under the Zen currency and its operation under the create operation.
    /// </summary>
    [Test]
    public async Task NewListingRecordsItsFeeOnceAndItsOperation()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        repository.Setup(r => r.ListItemAsync(It.IsAny<ItemListingDraft>(), It.IsAny<Func<ItemListingFacts, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuctionItemListingResult(AuctionItemListingOutcome.Created, Guid.NewGuid(), 1));
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = true });

        await service.ListItemAsync(ZenRequest());

        Assert.Multiple(() =>
        {
            Assert.That(this.Sum("auction.fees.removed", "currency", "Zen"), Is.EqualTo(10));
            Assert.That(this.Count("auction.operation.results", "operation", "create", "outcome", "Created"), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies a replayed listing records a replay, and no fee: the economic event happened once, on the first request.
    /// </summary>
    [Test]
    public async Task ReplayRecordsItsOutcomeButNoFee()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        repository.Setup(r => r.ListItemAsync(It.IsAny<ItemListingDraft>(), It.IsAny<Func<ItemListingFacts, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuctionItemListingResult(AuctionItemListingOutcome.Replayed, Guid.NewGuid(), 1));
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = true });

        await service.ListItemAsync(ZenRequest());

        Assert.Multiple(() =>
        {
            Assert.That(this.Count("auction.operation.results", "operation", "create", "outcome", "Replayed"), Is.EqualTo(1));
            Assert.That(this.Sum("auction.fees.removed", "currency", "Zen"), Is.EqualTo(0));
        });
    }

    private static AuctionListItemRequest ZenRequest()
    {
        return new AuctionListItemRequest(
            OperationId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            CharacterId: Guid.NewGuid(),
            SellerName: "Seller",
            SellerIp: "127.0.0.1",
            InventorySlot: 12,
            Currency: 0,
            Category: 0,
            StartingPrice: 1000,
            BuyoutPrice: null,
            DurationHours: 24);
    }

    private long Sum(string name, string key, string value)
    {
        lock (this._measurements)
        {
            return this._measurements.Where(m => m.Name == name && Equals(m.Tags.GetValueOrDefault(key), value)).Sum(m => m.Value);
        }
    }

    private int Count(string name, string key1, string value1, string key2, string value2)
    {
        lock (this._measurements)
        {
            return this._measurements.Count(m => m.Name == name && Equals(m.Tags.GetValueOrDefault(key1), value1) && Equals(m.Tags.GetValueOrDefault(key2), value2));
        }
    }
}
