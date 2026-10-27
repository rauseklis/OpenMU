// <copyright file="CashShopCatalogTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.CashShop;

using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.MessageHandler.CashShop;
using MUnique.OpenMU.Persistence.CashShop;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>Checks the shop catalogue against the real item data and the purchase / storage flow of the handler.</summary>
[TestFixture]
public class CashShopCatalogTests
{
    /// <summary>Every package of the shipped script can be bought, so nothing the client shows is a dead entry.</summary>
    [Test]
    public void EveryShippedPackageIsPurchasable()
    {
        var shipped = ReadResource("IBSPackage.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.That(CashShopCatalog.Default.PackageCount, Is.EqualTo(shipped));
        Assert.That(shipped, Is.GreaterThan(0));
    }

    /// <summary>All delivered items exist in a freshly created Season 6 database, at an allowed level.</summary>
    [Test]
    public async Task EveryDeliveredItemExistsInTheItemData()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new Persistence.Initialization.VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true);
        using var context = contextProvider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>()).First();

        foreach (var (group, number) in CashShopCatalog.Default.DeliveredItems)
        {
            Assert.That(configuration.Items.Any(i => i.Group == group && i.Number == number), Is.True, $"Item {group}/{number}");
        }
    }

    /// <summary>The price, currency and item come from the script, never from the client request.</summary>
    [Test]
    public void ResolveUsesScriptValues()
    {
        var catalog = CashShopCatalog.Default;

        Assert.That(catalog.TryResolve(122, 22, 0, out var offer), Is.True);
        Assert.That(offer.Currency, Is.EqualTo(CashShopCurrency.WCoinC));
        Assert.That((offer.ItemGroup, offer.ItemNumber), Is.EqualTo(((byte)13, (short)44)));
        Assert.That(offer.Price, Is.GreaterThan(0));
        Assert.That(offer.Quantity, Is.EqualTo(1));

        Assert.That(catalog.TryResolve(122, 23, 0, out _), Is.False, "wrong category");
        Assert.That(catalog.TryResolve(999999, 22, 0, out _), Is.False, "unknown package");
        Assert.That(catalog.TryResolve(122, 22, 999999, out _), Is.False, "unknown price option");
    }

    /// <summary>Every package delivers exactly one item per purchase (stacking is only supported for stackable items).</summary>
    [Test]
    public void EveryPackageDeliversOneItem()
    {
        var catalog = CashShopCatalog.Default;
        var packageSeqs = ReadResource("IBSPackage.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r').Split('@'));
        foreach (var fields in packageSeqs)
        {
            Assert.That(catalog.TryResolve(int.Parse(fields[2]), int.Parse(fields[0]), 0, out var offer), Is.True, fields[3]);
            Assert.That(offer.Quantity, Is.EqualTo(1), fields[3]);
        }
    }

    /// <summary>A purchase is paid with the currency of its tab and stored; a poor player gets the "not enough" answer.</summary>
    [Test]
    public async Task BuyDebitsTheScriptPriceAndReportsTheOutcome()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var accountId = CashShopProtocolTests.GiveIdentifiableAccount(player);
        player.GameContext.Configuration.Items.Add(CreateDefinition(13, 44));
        Assert.That(CashShopCatalog.Default.TryResolve(122, 22, 0, out var offer), Is.True);
        var request = BuyRequest(122, 22, 0);

        var repository = new Mock<ICashShopRepository>(MockBehavior.Strict);
        repository.Setup(r => r.PurchaseAsync(accountId, (byte)0, CashShopCurrency.WCoinC, offer.Price, It.Is<CashShopStorageItem>(i => i.ItemGroup == 13 && i.ItemNumber == 44 && i.PackageSeq == 122), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashShopPurchaseResult.Success);
        Assert.That((await CashShopRequests.HandleAsync(player, request, repository.Object, CashShopCatalog.Default, _ => true)).Single(), Is.EqualTo(CashShopProtocol.BuyResult(0)));

        repository.Setup(r => r.PurchaseAsync(accountId, (byte)0, CashShopCurrency.WCoinC, offer.Price, It.IsAny<CashShopStorageItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashShopPurchaseResult.InsufficientFunds);
        Assert.That((await CashShopRequests.HandleAsync(player, request, repository.Object, CashShopCatalog.Default, _ => true)).Single(), Is.EqualTo(CashShopProtocol.BuyResult(1)));
    }

    /// <summary>An item which is missing in the item data is refused before any currency is taken.</summary>
    [Test]
    public async Task BuyOfAnUnknownItemIsRefusedWithoutDebit()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        CashShopProtocolTests.GiveIdentifiableAccount(player);
        var repository = new Mock<ICashShopRepository>(MockBehavior.Strict);

        var response = await CashShopRequests.HandleAsync(player, BuyRequest(122, 22, 0), repository.Object, CashShopCatalog.Default, _ => true);

        Assert.That(response.Single(), Is.EqualTo(CashShopProtocol.BuyResult(4)));
        repository.VerifyNoOtherCalls();
    }

    /// <summary>The storage page lists nine items per page and a using request puts the item into the inventory.</summary>
    [Test]
    public async Task StorageListsPagesAndUseDeliversTheItem()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var accountId = CashShopProtocolTests.GiveIdentifiableAccount(player);
        player.GameContext.Configuration.Items.Add(CreateDefinition(13, 44));
        var items = Enumerable.Range(1, 11).Select(i => new CashShopStorageItem(i, 122, 7, 9, 13, 44, 0, 1)).ToList();
        var repository = new Mock<ICashShopRepository>(MockBehavior.Strict);
        repository.Setup(r => r.ListStorageAsync(accountId, (byte)0, It.IsAny<CancellationToken>())).ReturnsAsync(items);

        var page2 = await CashShopRequests.HandleAsync(player, ListRequest(2), repository.Object, CashShopCatalog.Default, _ => true);
        Assert.That(page2, Has.Count.EqualTo(3), "count packet plus two items");
        Assert.That(page2[0].AsSpan(4, 8).ToArray(), Is.EqualTo(new byte[] { 11, 0, 2, 0, 2, 0, 2, 0 }));
        Assert.That(page2[1][3], Is.EqualTo(0x0D));
        Assert.That(page2[1].Length, Is.EqualTo(33));

        repository.Setup(r => r.TakeStorageItemAsync(accountId, (byte)0, 3, It.IsAny<CancellationToken>())).ReturnsAsync(items[2]);
        var used = await CashShopRequests.HandleAsync(player, UseRequest(3), repository.Object, CashShopCatalog.Default, _ => true);
        Assert.That(used.Single(), Is.EqualTo(CashShopProtocol.UseResult(0)));
        Assert.That(player.Inventory!.Items.Any(i => i.Definition?.Number == 44), Is.True);
    }

    /// <summary>A missing storage item is reported and does not touch the inventory.</summary>
    [Test]
    public async Task UseOfAMissingStorageItemFails()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var accountId = CashShopProtocolTests.GiveIdentifiableAccount(player);
        var repository = new Mock<ICashShopRepository>(MockBehavior.Strict);
        repository.Setup(r => r.TakeStorageItemAsync(accountId, (byte)0, 5, It.IsAny<CancellationToken>())).ReturnsAsync((CashShopStorageItem?)null);

        var response = await CashShopRequests.HandleAsync(player, UseRequest(5), repository.Object, CashShopCatalog.Default, _ => true);

        Assert.That(response.Single(), Is.EqualTo(CashShopProtocol.UseResult(1)));
    }

    /// <summary>If the inventory cannot take the item, it goes back into the storage.</summary>
    [Test]
    public async Task UseWithoutRoomRestoresTheStorageItem()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var accountId = CashShopProtocolTests.GiveIdentifiableAccount(player);
        var stored = new CashShopStorageItem(4, 122, 7, 9, 13, 44, 0, 1);
        var repository = new Mock<ICashShopRepository>(MockBehavior.Strict);
        repository.Setup(r => r.TakeStorageItemAsync(accountId, (byte)0, 4, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        repository.Setup(r => r.RestoreStorageItemAsync(accountId, (byte)0, stored, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // The item definition is missing, so nothing can be created: the entry must not be lost.
        var response = await CashShopRequests.HandleAsync(player, UseRequest(4), repository.Object, CashShopCatalog.Default, _ => true);

        Assert.That(response.Single(), Is.EqualTo(CashShopProtocol.UseResult(21)));
        repository.VerifyAll();
    }

    private static DataModel.Configuration.Items.ItemDefinition CreateDefinition(byte group, short number)
        => new() { Group = group, Number = number, Width = 1, Height = 1, Durability = 1 };

    private static byte[] BuyRequest(int package, int category, int priceSeq)
    {
        var packet = new byte[23];
        packet[0] = 0xC1;
        packet[1] = 23;
        packet[2] = 0xD2;
        packet[3] = 3;
        BitConverter.GetBytes(package).CopyTo(packet, 4);
        BitConverter.GetBytes(category).CopyTo(packet, 8);
        BitConverter.GetBytes(priceSeq).CopyTo(packet, 12);
        return packet;
    }

    private static byte[] ListRequest(int page)
    {
        var packet = new byte[10];
        packet[0] = 0xC1;
        packet[1] = 10;
        packet[2] = 0xD2;
        packet[3] = 5;
        BitConverter.GetBytes(page).CopyTo(packet, 4);
        packet[8] = (byte)'S';
        return packet;
    }

    private static byte[] UseRequest(int id)
    {
        var packet = new byte[15];
        packet[0] = 0xC1;
        packet[1] = 15;
        packet[2] = 0xD2;
        packet[3] = 0x0B;
        BitConverter.GetBytes(id).CopyTo(packet, 4);
        BitConverter.GetBytes(id).CopyTo(packet, 8);
        return packet;
    }

    private static string ReadResource(string fileName)
    {
        var assembly = typeof(CashShopCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + fileName, StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!, System.Text.Encoding.Latin1);
        return reader.ReadToEnd();
    }
}
