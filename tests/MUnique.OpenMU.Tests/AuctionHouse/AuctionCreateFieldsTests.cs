// <copyright file="AuctionCreateFieldsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Threading;
using Moq;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Verifies the create result: an invalid note is refused as an invalid request, and a created listing returns its committed fee.
/// </summary>
[TestFixture]
public class AuctionCreateFieldsTests
{
    /// <summary>
    /// Verifies a note the repository refuses becomes an invalid request.
    /// </summary>
    [Test]
    public async Task InvalidNoteIsAnInvalidRequest()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        repository.Setup(r => r.ListItemAsync(It.IsAny<ItemListingDraft>(), It.IsAny<Func<ItemListingFacts, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuctionItemListingResult(AuctionItemListingOutcome.InvalidNote, Guid.Empty, 0));
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = true });

        var result = await service.ListItemAsync(Request());

        Assert.That(result.Result, Is.EqualTo(AuctionResultCode.InvalidRequest));
    }

    /// <summary>
    /// Verifies a created listing returns the fee that was charged, so the client shows the committed amount.
    /// </summary>
    [Test]
    public async Task CreatedListingReturnsTheCommittedFee()
    {
        var repository = new Mock<IAuctionHouseRepository>();
        repository.Setup(r => r.ListItemAsync(It.IsAny<ItemListingDraft>(), It.IsAny<Func<ItemListingFacts, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuctionItemListingResult(AuctionItemListingOutcome.Created, Guid.NewGuid(), 7));
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = true });

        var result = await service.ListItemAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.EqualTo(AuctionResultCode.Success));
            Assert.That(result.FeeScalar, Is.EqualTo(10));
        });
    }

    private static AuctionListItemRequest Request()
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
            DurationHours: 24,
            Note: "hello");
    }
}
