// <copyright file="JewelBankInventoryTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.JewelBank;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.JewelBank;

/// <summary>
/// Tests the counting of what a player carries which the Jewel Bank would take.
/// </summary>
[TestFixture]
public class JewelBankInventoryTests
{
    private static readonly byte First = (byte)InventoryConstants.EquippableSlotsCount;

    /// <summary>
    /// Plain jewels and fruits are counted per kind; items which a deposit would refuse are not.
    /// </summary>
    [Test]
    public async Task CountsPlainJewelsAndFruitsOnly()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await AddAsync(player, First, 14, 13, 0).ConfigureAwait(false);
        await AddAsync(player, (byte)(First + 1), 14, 13, 0).ConfigureAwait(false);
        await AddAsync(player, (byte)(First + 2), 13, 15, 3).ConfigureAwait(false);
        await AddAsync(player, (byte)(First + 3), 14, 13, 1).ConfigureAwait(false);
        await AddAsync(player, (byte)(First + 4), 14, 13, 0, durability: 4).ConfigureAwait(false);
        await AddAsync(player, (byte)(First + 5), 14, 13, 0, socketCount: 1).ConfigureAwait(false);
        await AddAsync(player, (byte)(First + 6), 0, 0, 0).ConfigureAwait(false);

        var counts = JewelBankInventory.Count(player);

        Assert.That(counts, Is.EquivalentTo(new Dictionary<byte, long> { [2] = 2, [14] = 1 }));
    }

    /// <summary>
    /// A player with nothing bankable has no counts.
    /// </summary>
    [Test]
    public async Task EmptyInventoryHasNoCounts()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        Assert.That(JewelBankInventory.Count(player), Is.Empty);
    }

    private static async ValueTask AddAsync(Player player, byte slot, byte group, short number, byte level, double durability = 1, int socketCount = 0)
    {
        var item = new Item
        {
            Definition = new ItemDefinition { Group = group, Number = number, Width = 1, Height = 1 },
            Level = level,
            Durability = durability,
            SocketCount = socketCount,
        };
        await player.Inventory!.AddItemAsync(slot, item).ConfigureAwait(false);
    }
}
