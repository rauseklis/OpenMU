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
        yield return new TestCaseData(ClientPacketDefinitions, "JewelBankDepositRequest", "01", "21", "4:Binary:OperationId[16];20:Byte:KindCode");
        yield return new TestCaseData(ClientPacketDefinitions, "JewelBankWithdrawRequest", "02", "25", "4:Binary:OperationId[16];20:Byte:KindCode;21:IntegerLittleEndian:Units");
        yield return new TestCaseData(ClientPacketDefinitions, "JewelBankSetAutoDepositRequest", "03", "5", "4:Byte:Enabled");
        yield return new TestCaseData(
            ServerPacketDefinitions,
            "JewelBankStateResponse",
            "80",
            "126",
            "4:Enum:Result;5:Byte:AutoDeposit;" + string.Join(';', Enumerable.Range(0, 15).Select(i => $"{6 + (8 * i)}:LongLittleEndian:Count{i + 1}")));
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
        Assert.That((string?)packet.Element(PacketDefinitionNamespace + "HeaderType"), Is.EqualTo("C1HeaderWithSubCode"));
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
