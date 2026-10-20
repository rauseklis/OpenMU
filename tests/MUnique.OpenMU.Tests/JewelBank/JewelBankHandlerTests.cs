// <copyright file="JewelBankHandlerTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.JewelBank;

using System.Reflection;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.GameServer.MessageHandler.JewelBank;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests the registration of the Jewel Bank packet handlers: their keys, their group, and that they are discoverable
/// plug-ins with identifiers of their own.
/// </summary>
[TestFixture]
public class JewelBankHandlerTests
{
    private static readonly Assembly GameServerAssembly = typeof(GroupPacketHandlerPlugIn).Assembly;

    /// <summary>
    /// The sub-codes match the packet definitions, which the client implements by hand.
    /// </summary>
    [Test]
    public void HandlersUseTheDefinedKeys()
    {
        Assert.Multiple(() =>
        {
            Assert.That(JewelBankGroupHandlerPlugIn.GroupKey, Is.EqualTo(0xD6));
            Assert.That(new JewelBankOpenHandlerPlugIn().Key, Is.EqualTo(0x00));
            Assert.That(new JewelBankDepositHandlerPlugIn().Key, Is.EqualTo(0x01));
            Assert.That(new JewelBankWithdrawHandlerPlugIn().Key, Is.EqualTo(0x02));
            Assert.That(new JewelBankSetAutoDepositHandlerPlugIn().Key, Is.EqualTo(0x03));
        });
    }

    /// <summary>
    /// Every request handler is a plug-in of the Jewel Bank group, and the packets are expected encrypted like the Auction House's.
    /// </summary>
    [Test]
    public void HandlersBelongToTheJewelBankGroup()
    {
        var handlers = JewelBankHandlerTypes();

        Assert.That(handlers, Has.Count.EqualTo(4));
        foreach (var type in handlers)
        {
            Assert.That(type.GetCustomAttribute<PlugInAttribute>(), Is.Not.Null, type.Name);
            Assert.That(type.GetCustomAttribute<BelongsToGroupAttribute>()?.GroupKey, Is.EqualTo(JewelBankGroupHandlerPlugIn.GroupKey), type.Name);
            Assert.That(((ISubPacketHandlerPlugIn)Activator.CreateInstance(type, nonPublic: true)!).IsEncryptionExpected, Is.True, type.Name);
        }
    }

    /// <summary>
    /// Plug-ins are identified by their GUID: the Jewel Bank ones must be unique among all plug-ins of the server, or one would replace another.
    /// </summary>
    [Test]
    public void PlugInIdentifiersAreUnique()
    {
        var all = GameServerAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<PlugInAttribute>() is not null)
            .Select(t => (Type: t, Guid: t.GetCustomAttribute<GuidAttribute>()?.Value))
            .ToList();
        var duplicates = all.Where(a => a.Guid is not null).GroupBy(a => a.Guid!.ToUpperInvariant()).Where(g => g.Count() > 1).ToList();

        Assert.That(duplicates.Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Type.Name))}"), Is.Empty);
        Assert.That(all.Where(a => a.Type.Namespace?.Contains("JewelBank") == true && a.Guid is null).Select(a => a.Type.Name), Is.Empty, "every Jewel Bank plug-in has a GUID");
    }

    private static List<Type> JewelBankHandlerTypes()
        => GameServerAssembly.GetTypes()
            .Where(t => typeof(ISubPacketHandlerPlugIn).IsAssignableFrom(t)
                        && t.GetCustomAttribute<BelongsToGroupAttribute>()?.GroupKey == JewelBankGroupHandlerPlugIn.GroupKey)
            .ToList();
}
