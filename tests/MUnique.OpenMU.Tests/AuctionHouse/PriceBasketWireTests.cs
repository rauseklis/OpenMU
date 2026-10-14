// <copyright file="PriceBasketWireTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the wire form of a price basket: thirteen little-endian 32-bit quantities in price-kind order.
/// </summary>
[TestFixture]
public class PriceBasketWireTests
{
    /// <summary>
    /// Verifies a basket is written as thirteen little-endian quantities and read back unchanged.
    /// </summary>
    [Test]
    public void BasketRoundTripsThroughItsWireForm()
    {
        var basket = PriceBasket.FromComponents(new[] { new PriceComponent(PriceKind.Zen, 250_000), new PriceComponent(PriceKind.Soul, 3), new PriceComponent(PriceKind.CommandFruit, 12) });
        var buffer = new byte[PriceBasketWire.Size];

        PriceBasketWire.Write(buffer, basket);

        Assert.Multiple(() =>
        {
            Assert.That(PriceBasketWire.Size, Is.EqualTo(52));
            Assert.That(buffer[..4], Is.EqualTo(new byte[] { 0x90, 0xD0, 0x03, 0x00 }), "250000 little-endian");
            Assert.That(buffer[12..16], Is.EqualTo(new byte[] { 3, 0, 0, 0 }), "Soul is kind 3");
            Assert.That(buffer[48..52], Is.EqualTo(new byte[] { 12, 0, 0, 0 }), "Command fruit is kind 12");
            Assert.That(PriceBasketWire.Read(buffer), Is.EqualTo(basket));
        });
    }

    /// <summary>
    /// Verifies a missing basket, or stored text which is not a basket, is written as all zeros, and that a short buffer reads as empty.
    /// </summary>
    [Test]
    public void MissingBasketIsWrittenAsZerosAndShortBufferReadsEmpty()
    {
        var buffer = new byte[PriceBasketWire.Size];
        Array.Fill(buffer, (byte)0xFF);

        PriceBasketWire.Write(buffer, "not a basket");

        Assert.Multiple(() =>
        {
            Assert.That(buffer, Is.All.EqualTo((byte)0));
            Assert.That(PriceBasketWire.Read(new byte[10]).IsEmpty, Is.True);
        });
    }

    /// <summary>
    /// Verifies stored text is written like the basket it holds.
    /// </summary>
    [Test]
    public void StoredTextIsWrittenLikeTheBasketItHolds()
    {
        var buffer = new byte[PriceBasketWire.Size];

        PriceBasketWire.Write(buffer, "50000,0,0,3,0,0,0,0,0,0,0,0,0");

        Assert.That(PriceBasketWire.Read(buffer).ToVectorText(), Is.EqualTo("50000,0,0,3,0,0,0,0,0,0,0,0,0"));
    }
}
