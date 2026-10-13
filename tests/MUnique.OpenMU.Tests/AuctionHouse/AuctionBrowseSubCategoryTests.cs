// <copyright file="AuctionBrowseSubCategoryTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Threading;
using Moq;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using Wire = MUnique.OpenMU.Network.Packets.ClientToServer;

/// <summary>
/// Verifies the sub-category travels from the browse packet to the repository search.
/// </summary>
[TestFixture]
public class AuctionBrowseSubCategoryTests
{
    /// <summary>
    /// Verifies the packet carries the sub-category at the end of its 105 bytes.
    /// </summary>
    [Test]
    public void TheBrowsePacketCarriesTheSubCategoryLast()
    {
        var packet = new byte[Wire.AuctionBrowseRequest.Length];
        var request = new Wire.AuctionBrowseRequest(packet);

        request.SubCategory = 5;

        Assert.Multiple(() =>
        {
            Assert.That(Wire.AuctionBrowseRequest.Length, Is.EqualTo(105));
            Assert.That(packet[104], Is.EqualTo((byte)5));
        });
    }

    /// <summary>
    /// Verifies the service keeps the sub-category when it forwards a browse to the repository.
    /// </summary>
    [Test]
    public async Task BrowseForwardsTheSubCategory()
    {
        AuctionListingSearch? seen = null;
        var repository = new Mock<IAuctionHouseRepository>();
        repository.Setup(r => r.SearchListingsAsync(It.IsAny<AuctionListingSearch>(), It.IsAny<CancellationToken>()))
            .Callback<AuctionListingSearch, CancellationToken>((search, _) => seen = search)
            .ReturnsAsync(default(AuctionListingPage));
        var service = new AuctionHouseService(repository.Object, new AuctionHouseOptions { Enabled = true });

        await service.BrowseAsync(new AuctionListingSearch(Currency: null, Category: 1, SubCategory: 3));

        Assert.That(seen?.SubCategory, Is.EqualTo((byte)3));
    }
}
