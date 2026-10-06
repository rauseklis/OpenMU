// <copyright file="AuctionHouseHandlerRegistrationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Reflection;
using MUnique.OpenMU.GameServer.MessageHandler;

/// <summary>
/// Verifies the Auction House packet handlers are registered under the 0xD5 group with the sub-codes of the protocol.
/// </summary>
[TestFixture]
public class AuctionHouseHandlerRegistrationTests
{
    private const string GameServerAssemblyName = "MUnique.OpenMU.GameServer";

    /// <summary>
    /// Verifies the grouped handlers answer the sub-codes of the protocol, and only those.
    /// </summary>
    [Test]
    public void SubHandlersUseTheProtocolSubCodes()
    {
        var expected = new Dictionary<string, byte>
        {
            ["AuctionOpenHandlerPlugIn"] = 0x00,
            ["AuctionBrowseHandlerPlugIn"] = 0x01,
            ["AuctionDetailHandlerPlugIn"] = 0x02,
            ["AuctionCreateHandlerPlugIn"] = 0x03,
            ["AuctionBidHandlerPlugIn"] = 0x04,
            ["AuctionBuyoutHandlerPlugIn"] = 0x05,
            ["AuctionCancelHandlerPlugIn"] = 0x06,
            ["AuctionCollectHandlerPlugIn"] = 0x09,
        };

        var keys = expected.Keys.ToDictionary(name => name, name => this.CreateSubHandler(name).Key);

        Assert.That(keys, Is.EqualTo(expected));
    }

    /// <summary>
    /// Verifies every Auction House sub-handler belongs to the 0xD5 group and expects encrypted packets.
    /// </summary>
    [Test]
    public void SubHandlersBelongToTheAuctionGroupAndExpectEncryption()
    {
        var groupKey = (byte)this.ResolveType("AuctionHouseGroupHandlerPlugIn").GetField("GroupKey", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var subHandlers = new[] { "AuctionOpenHandlerPlugIn", "AuctionBrowseHandlerPlugIn", "AuctionDetailHandlerPlugIn", "AuctionCreateHandlerPlugIn", "AuctionBidHandlerPlugIn", "AuctionBuyoutHandlerPlugIn", "AuctionCancelHandlerPlugIn", "AuctionCollectHandlerPlugIn" };

        Assert.Multiple(() =>
        {
            Assert.That(groupKey, Is.EqualTo(0xD5));
            foreach (var name in subHandlers)
            {
                var type = this.ResolveType(name);
                Assert.That(type.GetCustomAttribute<BelongsToGroupAttribute>()?.GroupKey, Is.EqualTo(groupKey), name);
                Assert.That(this.CreateSubHandler(name).IsEncryptionExpected, Is.True, name);
            }
        });
    }

    private Type ResolveType(string name)
    {
        return Type.GetType($"MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse.{name}, {GameServerAssemblyName}")
            ?? throw new InvalidOperationException($"{name} was not found in {GameServerAssemblyName}.");
    }

    private ISubPacketHandlerPlugIn CreateSubHandler(string name)
    {
        return (ISubPacketHandlerPlugIn)Activator.CreateInstance(this.ResolveType(name))!;
    }
}
