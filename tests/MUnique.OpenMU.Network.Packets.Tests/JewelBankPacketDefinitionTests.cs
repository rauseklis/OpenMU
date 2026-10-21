// <copyright file="JewelBankPacketDefinitionTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Packets.Tests;

using System.IO;
using System.Linq;
using System.Xml.Linq;

/// <summary>
/// Pins the Jewel Bank wire layouts (packet head 0xD6), which the game client implements by hand.
/// </summary>
[TestFixture]
public class JewelBankPacketDefinitionTests
{
    private const string ClientPacketDefinitions = "src/Network/Packets/ClientToServer/ClientToServerPackets.xml";
    private const string ServerPacketDefinitions = "src/Network/Packets/ServerToClient/ServerToClientPackets.xml";
    private static readonly XNamespace PacketDefinitionNamespace = "http://www.munique.net/OpenMU/PacketDefinitions";

    /// <summary>
    /// Gets the expected layouts: name, sub-code, length and fields as index:type:name.
    /// </summary>
    public static IEnumerable<TestCaseData> Cases()
    {
        yield return new TestCaseData(ClientPacketDefinitions, "JewelBankOpenRequest", "00", "4", string.Empty);
        yield return new TestCaseData(ClientPacketDefinitions, "JewelBankDepositRequest", "01", "201", "5:Binary:OperationId[16];21:Binary:Amounts[180]");
        yield return new TestCaseData(ClientPacketDefinitions, "JewelBankWithdrawRequest", "02", "201", "5:Binary:OperationId[16];21:Binary:Amounts[180]");
        yield return new TestCaseData(ClientPacketDefinitions, "JewelBankSetAutoDepositRequest", "03", "5", "4:Byte:Enabled");
        yield return new TestCaseData(
            ServerPacketDefinitions,
            "JewelBankStateResponse",
            "80",
            "367",
            "5:Enum:Result;6:Byte:AutoDeposit;7:Binary:Counts[180];187:Binary:Inventory[180]");
        yield return new TestCaseData(
            ServerPacketDefinitions,
            "JewelBankOperationResponse",
            "81",
            "27",
            "4:Binary:OperationId[16];20:Byte:Operation;21:Enum:Result;22:Byte:KindCode;23:IntegerLittleEndian:Units");
        yield return new TestCaseData(ServerPacketDefinitions, "JewelBankPickupNotification", "82", "17", "4:Byte:KindCode;5:IntegerLittleEndian:Units;9:LongLittleEndian:NewCount");
    }

    /// <summary>
    /// A packet keeps its sub-code, length and fields.
    /// </summary>
    /// <param name="file">The definition file, relative to the repository root.</param>
    /// <param name="name">The packet name.</param>
    /// <param name="subCode">The sub-code.</param>
    /// <param name="length">The length.</param>
    /// <param name="fields">The fields.</param>
    [TestCaseSource(nameof(Cases))]
    public void PacketLayoutIsPinned(string file, string name, string subCode, string length, string fields)
    {
        var packet = Load(file).Descendants(PacketDefinitionNamespace + "Packet").Single(p => (string?)p.Element(PacketDefinitionNamespace + "Name") == name);

        Assert.That((string?)packet.Element(PacketDefinitionNamespace + "Code"), Is.EqualTo("D6"));
        Assert.That((string?)packet.Element(PacketDefinitionNamespace + "SubCode"), Is.EqualTo(subCode));
        Assert.That((string?)packet.Element(PacketDefinitionNamespace + "Length"), Is.EqualTo(length));
        // Requests are encrypted (C3, or C4 when the encrypted packet would not fit a one-byte length), like the Auction House's; the group handler
        // expects that. Responses are plain C1 (C2 when longer than 255 bytes).
        // The state response is longer than 255 bytes and therefore a C2 packet.
        var isLongRequest = name is "JewelBankDepositRequest" or "JewelBankWithdrawRequest";
        var expectedHeader = file == ClientPacketDefinitions ? (isLongRequest ? "C4HeaderWithSubCode" : "C3HeaderWithSubCode") : name == "JewelBankStateResponse" ? "C2HeaderWithSubCode" : "C1HeaderWithSubCode";
        Assert.That((string?)packet.Element(PacketDefinitionNamespace + "HeaderType"), Is.EqualTo(expectedHeader));
        var actual = packet.Descendants(PacketDefinitionNamespace + "Field").Select(f =>
        {
            var fieldLength = (string?)f.Element(PacketDefinitionNamespace + "Length");
            return $"{(string?)f.Element(PacketDefinitionNamespace + "Index")}:{(string?)f.Element(PacketDefinitionNamespace + "Type")}:{(string?)f.Element(PacketDefinitionNamespace + "Name")}{(fieldLength is null ? string.Empty : $"[{fieldLength}]")}";
        });
        Assert.That(string.Join(';', actual), Is.EqualTo(fields));
    }

    /// <summary>
    /// No other packet may use the Jewel Bank head, and sub-codes are unique within each direction.
    /// </summary>
    [Test]
    public void SubCodesAreUniquePerDirection()
    {
        foreach (var file in new[] { ClientPacketDefinitions, ServerPacketDefinitions })
        {
            var subCodes = Load(file).Descendants(PacketDefinitionNamespace + "Packet")
                .Where(p => (string?)p.Element(PacketDefinitionNamespace + "Code") == "D6")
                .Select(p => (string?)p.Element(PacketDefinitionNamespace + "SubCode"))
                .ToList();
            Assert.That(subCodes, Is.Unique);
            Assert.That(subCodes, Is.Not.Empty);
        }
    }

    /// <summary>
    /// A C3 packet is SimpleModulus-encrypted: every 8 bytes of its content become 11 bytes, and its length is one byte. A request whose
    /// encrypted form is longer than 255 bytes must therefore be a C4 packet; sent as C3 the server rejects it ("unexpected content size")
    /// and drops the connection.
    /// </summary>
    [Test]
    public void EncryptedC3RequestsFitTheirOneByteLength()
    {
        var packets = Load(ClientPacketDefinitions).Descendants(PacketDefinitionNamespace + "Packet")
            .Where(p => (string?)p.Element(PacketDefinitionNamespace + "Code") == "D6" && (string?)p.Element(PacketDefinitionNamespace + "HeaderType") == "C3HeaderWithSubCode");
        foreach (var packet in packets)
        {
            var length = int.Parse((string?)packet.Element(PacketDefinitionNamespace + "Length") ?? "0");
            var encrypted = ((length - 2 + 1 + 7) / 8 * 11) + 2;
            Assert.That(encrypted, Is.LessThanOrEqualTo(255), (string?)packet.Element(PacketDefinitionNamespace + "Name"));
        }
    }

    private static XDocument Load(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, relativePath)))
        {
            directory = directory.Parent;
        }

        Assert.That(directory, Is.Not.Null, $"{relativePath} was not found above {AppContext.BaseDirectory}");
        return XDocument.Load(Path.Combine(directory!.FullName, relativePath));
    }
}
