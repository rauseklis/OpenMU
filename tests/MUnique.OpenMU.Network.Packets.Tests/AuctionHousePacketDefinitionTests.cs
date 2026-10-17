// <copyright file="AuctionHousePacketDefinitionTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Packets.Tests;

using System.IO;
using System.Linq;
using System.Xml.Linq;

/// <summary>
/// Verifies the Auction House wire layouts (packet head 0xD5) against the approved design specification.
/// </summary>
[TestFixture]
public class AuctionHousePacketDefinitionTests
{
    private const string ClientPacketDefinitions = "src/Network/Packets/ClientToServer/ClientToServerPackets.xml";
    private const string ServerPacketDefinitions = "src/Network/Packets/ServerToClient/ServerToClientPackets.xml";
    private const string CommonPacketDefinitions = "src/Network/Packets/CommonEnums.xml";
    private static readonly XNamespace PacketDefinitionNamespace = "http://www.munique.net/OpenMU/PacketDefinitions";

    /// <summary>
    /// Gets the client-to-server request layouts (C3 with sub-code).
    /// </summary>
    public static IEnumerable<TestCaseData> ClientRequestCases()
    {
        yield return new TestCaseData("AuctionOpenRequest", "00", "8", "4:IntegerLittleEndian:RequestId");
        yield return new TestCaseData(
            "AuctionBrowseRequest",
            "01",
            "105",
            "4:IntegerLittleEndian:RequestId;8:ShortLittleEndian:Page;10:Byte:Category;11:Enum:CurrencyMode;"
            + "12:Enum:Sort;13:IntegerLittleEndian:ClassMask;17:Byte:LevelMinimum;18:Byte:LevelMaximum;"
            + "19:ShortLittleEndian:OptionFlags;" + Amount(21, "Minimum") + ";" + Amount(45, "Maximum") + ";"
            + "69:ShortLittleEndian:MaxRemainingHours;71:Byte:NameLength;72:Binary:Name[32];104:Byte:SubCategory");
        yield return new TestCaseData("AuctionDetailRequest", "02", "16", "4:IntegerLittleEndian:RequestId;8:LongLittleEndian:ListingId");
        yield return new TestCaseData(
            "AuctionCreateRequest",
            "03",
            "257",
            "5:Binary:OperationId[16];21:Byte:InventorySlot;22:Enum:CurrencyMode;" + Amount(23, "Starting") + ";"
            + Amount(47, "Buyout") + ";71:Byte:DurationHours;72:Byte:NoteLength;73:Binary:Note[80];153:Binary:StartingBasket[52];205:Binary:BuyoutBasket[52]");
        yield return new TestCaseData(
            "AuctionBidRequest",
            "04",
            "108",
            "4:Binary:OperationId[16];20:LongLittleEndian:ListingId;28:IntegerLittleEndian:ExpectedVersion;" + Amount(32, "Bid") + ";56:Binary:BidBasket[52]");
        yield return new TestCaseData(
            "AuctionBuyoutRequest",
            "05",
            "32",
            "4:Binary:OperationId[16];20:LongLittleEndian:ListingId;28:IntegerLittleEndian:ExpectedVersion");
        yield return new TestCaseData(
            "AuctionCancelRequest",
            "06",
            "32",
            "4:Binary:OperationId[16];20:LongLittleEndian:ListingId;28:IntegerLittleEndian:ExpectedVersion");
        yield return new TestCaseData(
            "AuctionMyListingsRequest",
            "07",
            "11",
            "4:IntegerLittleEndian:RequestId;8:ShortLittleEndian:Page;10:Byte:StatusFilter");
        yield return new TestCaseData(
            "AuctionMailboxRequest",
            "08",
            "11",
            "4:IntegerLittleEndian:RequestId;8:ShortLittleEndian:Page;10:Byte:KindFilter");
        yield return new TestCaseData(
            "AuctionCollectRequest",
            "09",
            "56",
            "4:Binary:OperationId[16];20:LongLittleEndian:CollectionId;28:IntegerLittleEndian:ExpectedVersion;"
            + Amount(32, "Requested"));
    }

    /// <summary>
    /// Verifies a client-to-server Auction House request against the design's C3 sub-code layout.
    /// </summary>
    /// <param name="name">The packet name.</param>
    /// <param name="subCode">The sub-code.</param>
    /// <param name="length">The total packet length.</param>
    /// <param name="fields">The field layout.</param>
    [TestCaseSource(nameof(ClientRequestCases))]
    public void ClientRequestMatchesDesign(string name, string subCode, string length, string fields)
    {
        // A C3 packet stores its length in one byte, so it can be at most 255 bytes long. The 257 byte create request
        // therefore needs the C4 header, whose length is two bytes.
        var header = name == "AuctionCreateRequest" ? "C4HeaderWithSubCode" : "C3HeaderWithSubCode";
        AssertPacketDefinition(ClientPacketDefinitions, name, header, "D5", subCode, length, fields);
    }

    /// <summary>
    /// Gets the server-to-client response layouts.
    /// </summary>
    public static IEnumerable<TestCaseData> ServerResponseCases()
    {
        yield return new TestCaseData(
            "AuctionOpenResponse",
            "C2HeaderWithSubCode",
            "80",
            string.Empty,
            "5:IntegerLittleEndian:RequestId;9:Enum:Result;10:IntegerLittleEndian:ServerTime;"
            + "14:IntegerLittleEndian:ConfigurationVersion;18:ShortLittleEndian:ListingFeeBasisPoints;"
            + "20:ShortLittleEndian:SuccessFeeBasisPoints;22:Byte:DurationMask;23:Byte:DefaultDurationHours;"
            + "24:Byte:PageSize;25:ShortLittleEndian:PendingMailboxCount;27:Byte:CurrencyCount;"
            + "28:IntegerLittleEndian:SaleFee;32:Byte:MixedPricesEnabled;33:Structure[]:Currencies");
        yield return new TestCaseData(
            "AuctionBrowseResponse",
            "C2HeaderWithSubCode",
            "81",
            string.Empty,
            "5:IntegerLittleEndian:RequestId;9:Enum:Result;10:ShortLittleEndian:Page;12:ShortLittleEndian:TotalPages;"
            + "14:IntegerLittleEndian:TotalCount;18:IntegerLittleEndian:ServerTime;22:Byte:ListingCount;"
            + "23:Structure[]:Listings");
        yield return new TestCaseData(
            "AuctionDetailResponse",
            "C2HeaderWithSubCode",
            "82",
            "360",
            "5:IntegerLittleEndian:RequestId;9:Enum:Result;10:IntegerLittleEndian:ServerTime;"
            + "14:LongLittleEndian:ListingId;22:IntegerLittleEndian:Version;26:Enum:Status;27:Enum:CurrencyMode;"
            + "28:Enum:Category;" + Amount(29, "CurrentPrice") + ";" + Amount(53, "BuyoutPrice") + ";"
            + "77:ShortLittleEndian:BidCount;79:IntegerLittleEndian:OriginalEndsAt;83:IntegerLittleEndian:EndsAt;"
            + "87:Binary:SellerName[10];97:Binary:CurrentBidderName[10];107:Byte:NoteLength;108:Binary:Note[80];"
            + "188:Byte:ItemDataLength;189:Binary:ItemData[15];204:Binary:StartingBasket[52];256:Binary:CurrentBasket[52];308:Binary:BuyoutBasket[52]");
        yield return new TestCaseData(
            "AuctionOperationResponse",
            "C1HeaderWithSubCode",
            "83",
            "143",
            "4:Binary:OperationId[16];20:Enum:OperationType;21:Enum:Result;22:LongLittleEndian:ListingId;"
            + "30:LongLittleEndian:CollectionId;38:IntegerLittleEndian:ListingVersion;42:Enum:CurrencyMode;"
            + Amount(43, "Fee") + ";" + Amount(67, "Spendable") + ";" + Amount(91, "Claimed") + ";"
            + Amount(115, "Remaining") + ";139:IntegerLittleEndian:ServerTime");
        yield return new TestCaseData(
            "AuctionMyListingsResponse",
            "C2HeaderWithSubCode",
            "84",
            string.Empty,
            "5:IntegerLittleEndian:RequestId;9:Enum:Result;10:ShortLittleEndian:Page;12:ShortLittleEndian:TotalPages;"
            + "14:IntegerLittleEndian:TotalCount;18:IntegerLittleEndian:ServerTime;22:Byte:ListingCount;"
            + "23:Structure[]:Listings");
        yield return new TestCaseData(
            "AuctionMailboxResponse",
            "C2HeaderWithSubCode",
            "85",
            string.Empty,
            "5:IntegerLittleEndian:RequestId;9:Enum:Result;10:ShortLittleEndian:Page;12:ShortLittleEndian:TotalPages;"
            + "14:IntegerLittleEndian:TotalCount;18:IntegerLittleEndian:ServerTime;22:Byte:EntryCount;"
            + "23:Structure[]:Entries");
        yield return new TestCaseData(
            "AuctionNotification",
            "C1HeaderWithSubCode",
            "86",
            "40",
            "4:Enum:NotificationKind;5:LongLittleEndian:ListingId;13:Enum:CurrencyMode;" + Amount(14, "Amount")
            + ";38:ShortLittleEndian:PendingMailboxCount");
    }

    /// <summary>
    /// Verifies a server-to-client Auction House response or notification against the design's layout.
    /// </summary>
    /// <param name="name">The packet name.</param>
    /// <param name="headerType">The header type (C1 small, C2 paged/detailed).</param>
    /// <param name="subCode">The sub-code.</param>
    /// <param name="length">The total length, or empty for variable-length packets.</param>
    /// <param name="fields">The field layout.</param>
    [TestCaseSource(nameof(ServerResponseCases))]
    public void ServerResponseMatchesDesign(string name, string headerType, string subCode, string length, string fields)
    {
        AssertPacketDefinition(ServerPacketDefinitions, name, headerType, "D5", subCode, length, fields);
    }

    /// <summary>
    /// Gets the fixed-size nested structures of paged Auction House responses.
    /// </summary>
    public static IEnumerable<TestCaseData> NestedStructureCases()
    {
        yield return new TestCaseData(
            "AuctionOpenResponse",
            "AuctionCurrencyDescriptor",
            "72",
            "0:Enum:CurrencyMode;1:Boolean:Enabled;2:IntegerLittleEndian:MinimumStartingPrice;"
            + "6:IntegerLittleEndian:MaximumStartingPrice;10:IntegerLittleEndian:MinimumIncrement;"
            + "14:Byte:IncrementPercent;" + Amount(15, "Spendable") + ";"
            + "39:ShortLittleEndian:SmallPackUnits;41:ShortLittleEndian:MediumPackUnits;"
            + "43:ShortLittleEndian:LargePackUnits;45:ShortLittleEndian:LooseItemType;"
            + "47:ShortLittleEndian:PackItemType;49:ShortLittleEndian:LooseItemCount;"
            + "51:ShortLittleEndian:SmallPackCount;53:ShortLittleEndian:MediumPackCount;"
            + "55:ShortLittleEndian:LargePackCount;57:ShortLittleEndian:StrengthFruitItemType;"
            + "59:ShortLittleEndian:AgilityFruitItemType;61:ShortLittleEndian:VitalityFruitItemType;"
            + "63:ShortLittleEndian:EnergyFruitItemType;65:ShortLittleEndian:CommandFruitItemType;"
            + "67:Byte:StrengthFruitItemLevel;68:Byte:AgilityFruitItemLevel;69:Byte:VitalityFruitItemLevel;"
            + "70:Byte:EnergyFruitItemLevel;71:Byte:CommandFruitItemLevel");
        yield return new TestCaseData(
            "AuctionBrowseResponse",
            "AuctionListingSummary",
            "199",
            "0:LongLittleEndian:ListingId;8:IntegerLittleEndian:Version;12:Enum:Status;13:Enum:CurrencyMode;"
            + "14:Enum:Category;" + Amount(15, "CurrentPrice") + ";" + Amount(39, "BuyoutPrice") + ";"
            + "63:ShortLittleEndian:BidCount;65:IntegerLittleEndian:EndsAt;69:Binary:SellerName[10];"
            + "79:Byte:ItemDataLength;80:Binary:ItemData[15];95:Binary:CurrentBasket[52];147:Binary:BuyoutBasket[52]");
        yield return new TestCaseData(
            "AuctionMyListingsResponse",
            "AuctionListingSummary",
            "199",
            "0:LongLittleEndian:ListingId;8:IntegerLittleEndian:Version;12:Enum:Status;13:Enum:CurrencyMode;"
            + "14:Enum:Category;" + Amount(15, "CurrentPrice") + ";" + Amount(39, "BuyoutPrice") + ";"
            + "63:ShortLittleEndian:BidCount;65:IntegerLittleEndian:EndsAt;69:Binary:SellerName[10];"
            + "79:Byte:ItemDataLength;80:Binary:ItemData[15];95:Binary:CurrentBasket[52];147:Binary:BuyoutBasket[52]");
        yield return new TestCaseData(
            "AuctionMailboxResponse",
            "AuctionMailboxEntry",
            "192",
            "0:LongLittleEndian:CollectionId;8:Enum:CollectionKind;9:Enum:CollectionStatus;10:Enum:CurrencyMode;"
            + Amount(11, "Original") + ";" + Amount(35, "Remaining") + ";59:LongLittleEndian:SourceListingId;"
            + "67:Boolean:HasItem;68:Byte:ItemDataLength;69:Binary:ItemData[15];84:IntegerLittleEndian:Version;88:Binary:OriginalBasket[52];140:Binary:RemainingBasket[52]");
    }

    /// <summary>
    /// Verifies the fixed-size structures nested in paged Auction House responses.
    /// </summary>
    /// <param name="packetName">The packet which owns the structure.</param>
    /// <param name="structureName">The structure name.</param>
    /// <param name="length">The fixed element length.</param>
    /// <param name="fields">The field layout.</param>
    [TestCaseSource(nameof(NestedStructureCases))]
    public void NestedStructureMatchesDesign(string packetName, string structureName, string length, string fields)
    {
        var definitions = LoadPacketDefinitions(ServerPacketDefinitions);
        var packet = FindPacket(definitions, packetName);
        var structure = packet.Element(PacketDefinitionNamespace + "Structures")!
            .Elements(PacketDefinitionNamespace + "Structure")
            .Single(element => element.Element(PacketDefinitionNamespace + "Name")!.Value == structureName);

        Assert.Multiple(() =>
        {
            Assert.That(structure.Element(PacketDefinitionNamespace + "Length")!.Value, Is.EqualTo(length));
            Assert.That(GetFieldLayout(structure), Is.EqualTo(fields));
        });
    }

    /// <summary>
    /// Gets the shared Auction House enum values, which the client maps to localized text.
    /// </summary>
    public static IEnumerable<TestCaseData> EnumCases()
    {
        yield return new TestCaseData("AuctionResult", "Success=0;Disabled=1;NotAuthenticated=2;StateRestricted=3;LevelTooLow=4;AccountTooNew=5;AccountRestricted=6;RateLimited=7;InvalidRequest=8;UnsupportedCurrency=9;CurrencyConfigurationInvalid=10;InvalidDuration=11;InvalidPrice=12;InvalidFruitBasket=13;ItemNotFound=14;ItemNotEligible=15;ListingLimitReached=16;InsufficientCurrency=17;ListingNotFound=18;ListingNotActive=19;ListingExpired=20;StaleListing=21;SelfTrade=22;AlreadyHighestBidder=23;BidTooLow=24;BuyoutUnavailable=25;CannotCancelWithBid=26;CollectionNotFound=27;InvalidClaimQuantity=28;InventoryFull=29;MoneyLimit=30;AlreadyProcessed=31;ServiceUnavailable=32");
        yield return new TestCaseData("AuctionCurrencyMode", "Zen=0;Chaos=1;Bless=2;Soul=3;Life=4;Creation=5;Guardian=6;Harmony=7;Fruits=8;Mixed=9");
        yield return new TestCaseData("AuctionCategory", "Weapon=0;Armor=1;Wing=2;PetHelper=3;JewelMaterial=4;Consumable=5;Miscellaneous=6;ScrollOrb=7");
        yield return new TestCaseData("AuctionSort", "PriceAscending=0;PriceDescending=1;EndingSoonest=2;Newest=3");
        yield return new TestCaseData("AuctionListingStatus", "Active=0;Sold=1;Expired=2;Cancelled=3;AdminRemoved=4");
        yield return new TestCaseData("AuctionCollectionKind", "PurchasedItem=0;ReturnedItem=1;SaleProceeds=2;OutbidRefund=3;TenderChange=4;AdminRefund=5;FeeRefund=6");
        yield return new TestCaseData("AuctionCollectionStatus", "Pending=0;PartiallyClaimed=1;Claimed=2");
        yield return new TestCaseData("AuctionOperationType", "Create=0;Bid=1;Buyout=2;Cancel=3;Collect=4");
        yield return new TestCaseData("AuctionNotificationKind", "Outbid=0;Sold=1;Bought=2;Expired=3;Returned=4;Collected=5;AdminAdjusted=6");
    }

    /// <summary>
    /// Verifies the shared Auction House enum values.
    /// </summary>
    /// <param name="enumName">The enum name.</param>
    /// <param name="values">The expected values.</param>
    [TestCaseSource(nameof(EnumCases))]
    public void SharedEnumMatchesDesign(string enumName, string values)
    {
        var definitions = LoadPacketDefinitions(CommonPacketDefinitions);
        var enumDefinition = definitions.Root!
            .Element(PacketDefinitionNamespace + "Enums")!
            .Elements(PacketDefinitionNamespace + "Enum")
            .Single(element => element.Element(PacketDefinitionNamespace + "Name")!.Value == enumName);

        var actualValues = string.Join(
            ';',
            enumDefinition.Element(PacketDefinitionNamespace + "Values")!
                .Elements(PacketDefinitionNamespace + "EnumValue")
                .Select(value => $"{value.Element(PacketDefinitionNamespace + "Name")!.Value}={value.Element(PacketDefinitionNamespace + "Value")!.Value}"));

        Assert.That(actualValues, Is.EqualTo(values));
    }

    /// <summary>
    /// Verifies that head 0xD5 is used exclusively by the Auction House and that its sub-codes are unique.
    /// </summary>
    [Test]
    public void HeadD5IsReservedForAuctionHouseWithUniqueSubCodes()
    {
        var expectedClient = new[] { "00", "01", "02", "03", "04", "05", "06", "07", "08", "09" };
        var expectedServer = new[] { "80", "81", "82", "83", "84", "85", "86" };

        var clientCodes = GetPacketsWithCode(ClientPacketDefinitions, "D5")
            .Select(packet => packet.Element(PacketDefinitionNamespace + "SubCode")!.Value)
            .ToArray();
        var serverCodes = GetPacketsWithCode(ServerPacketDefinitions, "D5")
            .Select(packet => packet.Element(PacketDefinitionNamespace + "SubCode")!.Value)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(clientCodes.Distinct().Count(), Is.EqualTo(clientCodes.Length));
            Assert.That(serverCodes.Distinct().Count(), Is.EqualTo(serverCodes.Length));
            Assert.That(clientCodes.OrderBy(code => code), Is.EqualTo(expectedClient));
            Assert.That(serverCodes.OrderBy(code => code), Is.EqualTo(expectedServer));
        });
    }

    private static string Amount(int start, string prefix)
        => string.Join(
            ";",
            $"{start}:IntegerLittleEndian:{prefix}Scalar",
            $"{start + 4}:IntegerLittleEndian:{prefix}Strength",
            $"{start + 8}:IntegerLittleEndian:{prefix}Agility",
            $"{start + 12}:IntegerLittleEndian:{prefix}Vitality",
            $"{start + 16}:IntegerLittleEndian:{prefix}Energy",
            $"{start + 20}:IntegerLittleEndian:{prefix}Command");

    private static XDocument LoadPacketDefinitions(string relativePath)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(path))
            {
                return XDocument.Load(path);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate '{relativePath}'.");
    }

    private static XElement FindPacket(XDocument definitions, string packetName)
        => definitions.Root!
            .Element(PacketDefinitionNamespace + "Packets")!
            .Elements(PacketDefinitionNamespace + "Packet")
            .Single(element => element.Element(PacketDefinitionNamespace + "Name")!.Value == packetName);

    private static IEnumerable<XElement> GetPacketsWithCode(string relativePath, string code)
        => LoadPacketDefinitions(relativePath).Root!
            .Element(PacketDefinitionNamespace + "Packets")!
            .Elements(PacketDefinitionNamespace + "Packet")
            .Where(packet => packet.Element(PacketDefinitionNamespace + "Code")?.Value == code);

    private static string GetFieldLayout(XElement parent)
        => string.Join(
            ';',
            parent.Element(PacketDefinitionNamespace + "Fields")!
                .Elements(PacketDefinitionNamespace + "Field")
                .Select(field =>
                {
                    var length = field.Element(PacketDefinitionNamespace + "Length")?.Value;
                    var lengthSuffix = length is null ? string.Empty : $"[{length}]";
                    var index = field.Element(PacketDefinitionNamespace + "Index")!.Value;
                    var type = field.Element(PacketDefinitionNamespace + "Type")!.Value;
                    var name = field.Element(PacketDefinitionNamespace + "Name")!.Value;
                    return $"{index}:{type}:{name}{lengthSuffix}";
                }));

    /// <summary>
    /// A C1 or C3 packet stores its length in a single byte. A longer packet is written with a wrapped length (256 becomes 0),
    /// which the receiving side treats as a broken stream and closes the connection. Every fixed-length packet in both
    /// directions must therefore use a C2 or C4 header once it is longer than 255 bytes.
    /// </summary>
    /// <param name="relativePath">The packet definition file.</param>
    [TestCase(ClientPacketDefinitions)]
    [TestCase(ServerPacketDefinitions)]
    public void FixedLengthPacketsFitTheirHeader(string relativePath)
    {
        var tooLong = LoadPacketDefinitions(relativePath)
            .Descendants(PacketDefinitionNamespace + "Packet")
            .Where(packet => (packet.Element(PacketDefinitionNamespace + "HeaderType")?.Value ?? string.Empty).StartsWith("C1", StringComparison.Ordinal)
                || (packet.Element(PacketDefinitionNamespace + "HeaderType")?.Value ?? string.Empty).StartsWith("C3", StringComparison.Ordinal))
            .Where(packet => int.TryParse(packet.Element(PacketDefinitionNamespace + "Length")?.Value, out var length) && length > 255)
            .Select(packet => packet.Element(PacketDefinitionNamespace + "Name")!.Value)
            .ToList();

        Assert.That(tooLong, Is.Empty, "These C1/C3 packets are longer than 255 bytes: " + string.Join(", ", tooLong));
    }

    private static void AssertPacketDefinition(
        string relativePath,
        string name,
        string headerType,
        string code,
        string subCode,
        string length,
        string fields)
    {
        var definitions = LoadPacketDefinitions(relativePath);
        var packet = FindPacket(definitions, name);

        Assert.Multiple(() =>
        {
            Assert.That(packet.Element(PacketDefinitionNamespace + "HeaderType")!.Value, Is.EqualTo(headerType));
            Assert.That(packet.Element(PacketDefinitionNamespace + "Code")!.Value, Is.EqualTo(code));
            Assert.That(packet.Element(PacketDefinitionNamespace + "SubCode")!.Value, Is.EqualTo(subCode));
            Assert.That(packet.Element(PacketDefinitionNamespace + "Length")?.Value ?? string.Empty, Is.EqualTo(length));
            Assert.That(GetFieldLayout(packet), Is.EqualTo(fields));
        });
    }
}
