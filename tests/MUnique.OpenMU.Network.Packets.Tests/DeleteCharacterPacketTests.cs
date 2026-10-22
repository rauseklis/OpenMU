// <copyright file="DeleteCharacterPacketTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Packets.Tests;

using MUnique.OpenMU.Network.Packets.ClientToServer;

/// <summary>
/// Tests the layout of the <see cref="DeleteCharacter"/> packet, which carries the account password.
/// </summary>
[TestFixture]
public class DeleteCharacterPacketTests
{
    /// <summary>
    /// Tests that the packet has room for a whole account password of 20 characters.
    /// </summary>
    [Test]
    public void CarriesAWholePassword()
    {
        const string password = "12345678901234567890";
        var data = new byte[DeleteCharacterRef.Length];
        var packet = new DeleteCharacterRef(data)
        {
            Name = "Gody",
            SecurityCode = password,
        };

        Assert.That(DeleteCharacterRef.Length, Is.EqualTo(34));
        Assert.That(packet.SecurityCode, Is.EqualTo(password));
        Assert.That(new DeleteCharacter(data).SecurityCode, Is.EqualTo(password));
    }

    /// <summary>
    /// Tests that the shorter packet of an older client (10 characters of password) is still read completely.
    /// </summary>
    [Test]
    public void ReadsTheShorterPacketOfAnOlderClient()
    {
        var data = new byte[24];
        data[0] = 0xC1;
        data[1] = 24;
        data[2] = 0xF3;
        data[3] = 0x02;
        var packet = new DeleteCharacter(data)
        {
            Name = "Gody",
            SecurityCode = "1234567890",
        };

        Assert.That(packet.SecurityCode, Is.EqualTo("1234567890"));
    }
}
