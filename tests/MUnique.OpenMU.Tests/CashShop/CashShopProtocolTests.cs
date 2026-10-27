// <copyright file="CashShopProtocolTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.CashShop;

using System.Buffers.Binary;
using System.Threading;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.GameServer.MessageHandler.CashShop;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.CashShop;

/// <summary>Exercises existing client wire fields and the authenticated read-only request path.</summary>
[TestFixture]
public class CashShopProtocolTests
{
    /// <summary>WC, WP and GP occupy the installed client's exact packed double fields.</summary>
    [Test]
    public void BalanceMatchesNativeClientLayout()
    {
        var packet = CashShopProtocol.Balance(new CashShopBalance(123, 456, 789));
        Assert.That(packet[..5], Is.EqualTo(new byte[] { 0xC1, 45, 0xD2, 1, 0 }));
        Assert.That(new[] { ReadDouble(packet, 5), ReadDouble(packet, 13), ReadDouble(packet, 21), ReadDouble(packet, 29), ReadDouble(packet, 37) },
            Is.EqualTo(new double[] { 579, 123, 456, 0, 789 }));
    }

    /// <summary>Invalid framing and unsupported subcodes cannot reach persistence.</summary>
    [TestCase(new byte[] { })]
    [TestCase(new byte[] { 0xC1, 4, 0xD2 })]
    [TestCase(new byte[] { 0xC1, 5, 0xD2, 1 })]
    [TestCase(new byte[] { 0xC3, 4, 0xD2, 1 })]
    [TestCase(new byte[] { 0xC1, 4, 0xD3, 1 })]
    [TestCase(new byte[] { 0xC1, 4, 0xD2, 0xFF })]
    [TestCase(new byte[] { 0xC1, 5, 0xD2, 2, 2 })]
    public void MalformedRequestsAreRejected(byte[] packet)
        => Assert.That(CashShopProtocol.IsValidRequest(packet), Is.False);

    /// <summary>The empty storage and unsupported purchase replies never report delivered items.</summary>
    [Test]
    public void StorageIsEmptyAndPurchasesAreRefused()
    {
        Assert.That(CashShopProtocol.EmptyStorage(), Is.EqualTo(new byte[] { 0xC1, 12, 0xD2, 6, 0, 0, 0, 0, 1, 0, 1, 0 }));
        Assert.That(CashShopProtocol.Refused(3), Is.EqualTo(new byte[] { 0xC1, 9, 0xD2, 3, 6, 0, 0, 0, 0 }));
        Assert.That(new CashShopPacketHandlerPlugIn().Key, Is.EqualTo(0xD2));
        Assert.That(new CashShopPacketHandlerPlugIn().IsEncryptionExpected, Is.False);
    }

    /// <summary>A valid request reads the authenticated account and builds the actual response.</summary>
    [Test]
    public async Task BalanceRequestUsesAuthenticatedIdentity()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var accountId = GiveIdentifiableAccount(player);
        var repository = new Mock<ICashShopRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetBalanceAsync(accountId, (byte)0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CashShopBalance(10_000, 20_000, 30_000));

        var response = await CashShopRequests.HandleAsync(player, new byte[] { 0xC1, 4, 0xD2, 1 }, repository.Object);

        Assert.That(response, Is.Not.Null);
        Assert.That(ReadDouble(response!, 13), Is.EqualTo(10_000));
        Assert.That(ReadDouble(response!, 21), Is.EqualTo(20_000));
        Assert.That(ReadDouble(response!, 37), Is.EqualTo(30_000));
        repository.VerifyAll();
    }

    /// <summary>Closing does not reopen the window or touch persistence.</summary>
    [Test]
    public async Task CloseAndUnsupportedPurchaseDoNotMutateBalance()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        GiveIdentifiableAccount(player);
        var repository = new Mock<ICashShopRepository>(MockBehavior.Strict);

        Assert.That(await CashShopRequests.HandleAsync(player, new byte[] { 0xC1, 5, 0xD2, 2, 1 }, repository.Object), Is.Null);
        Assert.That(await CashShopRequests.HandleAsync(player, new byte[] { 0xC1, 5, 0xD2, 3, 0 }, repository.Object),
            Is.EqualTo(CashShopProtocol.Refused(3)));
        repository.VerifyNoOtherCalls();
    }

    /// <summary>Unauthenticated players cannot read another account's wallet or open the shop.</summary>
    [Test]
    public async Task MissingAccountDeniesOpeningAndReading()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        player.Account = null;
        var repository = new Mock<ICashShopRepository>(MockBehavior.Strict);

        Assert.That(await CashShopRequests.HandleAsync(player, new byte[] { 0xC1, 5, 0xD2, 2, 0 }, repository.Object),
            Is.EqualTo(CashShopProtocol.Open(false)));
        Assert.That(await CashShopRequests.HandleAsync(player, new byte[] { 0xC1, 4, 0xD2, 1 }, repository.Object), Is.Null);
        repository.VerifyNoOtherCalls();
    }

    private static Guid GiveIdentifiableAccount(Player player)
    {
        var id = Guid.NewGuid();
        var account = new Mock<Account>();
        account.As<Persistence.IIdentifiable>().SetupGet(a => a.Id).Returns(id);
        account.Setup(a => a.Attributes).Returns(new List<StatAttribute>());
        account.Setup(a => a.UnlockedCharacterClasses).Returns(new List<CharacterClass>());
        player.Account = account.Object;
        return id;
    }

    private static double ReadDouble(byte[] packet, int offset)
        => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(packet.AsSpan(offset)));
}
