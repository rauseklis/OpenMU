// <copyright file="PlayerInventoryRefreshTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Moq;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Persistence.AuctionHouse;
using BasicItem = MUnique.OpenMU.Persistence.BasicModel.Item;

/// <summary>
/// Tests of <see cref="PlayerInventoryRefresh"/>: after the Auction House changed the committed inventory, the live player
/// follows without reconnecting.
/// </summary>
[TestFixture]
public class PlayerInventoryRefreshTests
{
    private const byte FirstSlot = 20;

    /// <summary>
    /// An item which is no longer in the committed inventory leaves the live inventory, and the client gets the new list.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemGoneFromTheDatabaseLeavesTheLiveInventoryAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var listed = NewItem(FirstSlot);
        var kept = NewItem(FirstSlot + 1);
        await player.Inventory!.AddItemAsync(FirstSlot, listed).ConfigureAwait(false);
        await player.Inventory.AddItemAsync(FirstSlot + 1, kept).ConfigureAwait(false);

        ResetViews(player);
        var changed = await PlayerInventoryRefresh.ApplyAsync(player, State(0, kept)).ConfigureAwait(false);

        Assert.That(changed, Is.True);
        Assert.That(player.Inventory.Items, Is.EquivalentTo(new[] { kept }));
        Assert.That(player.Inventory.GetItem(FirstSlot), Is.Null);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IUpdateInventoryListPlugIn>()!).Verify(v => v.UpdateInventoryListAsync(), Times.Once);
    }

    /// <summary>
    /// An item which arrived in the committed inventory is loaded and placed into its slot.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemArrivedInTheDatabaseIsAddedToTheLiveInventoryAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var arrived = NewItem(FirstSlot + 3);

        ResetViews(player);
        var changed = await PlayerInventoryRefresh.ApplyAsync(player, State(0, arrived), id => Task.FromResult<Item?>(id == ((BasicItem)arrived).Id ? arrived : null)).ConfigureAwait(false);

        Assert.That(changed, Is.True);
        Assert.That(player.Inventory!.GetItem(FirstSlot + 3), Is.SameAs(arrived));
        Mock.Get(player.ViewPlugIns.GetPlugIn<IUpdateInventoryListPlugIn>()!).Verify(v => v.UpdateInventoryListAsync(), Times.Once);
    }

    /// <summary>
    /// A changed stack is replaced by the committed copy.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ItemWithOtherDurabilityIsReplacedByTheCommittedCopyAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var stale = NewItem(FirstSlot, durability: 5);
        await player.Inventory!.AddItemAsync(FirstSlot, stale).ConfigureAwait(false);
        var fresh = NewItem(FirstSlot, durability: 2);
        ((BasicItem)fresh).Id = ((BasicItem)stale).Id;

        ResetViews(player);
        await PlayerInventoryRefresh.ApplyAsync(player, State(0, fresh), _ => Task.FromResult<Item?>(fresh)).ConfigureAwait(false);

        Assert.That(player.Inventory.GetItem(FirstSlot), Is.SameAs(fresh));
        Assert.That(player.Inventory.Items.Count(), Is.EqualTo(1));
    }

    /// <summary>
    /// The Zen of the live character becomes the committed Zen, and the client is told.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ZenFollowsTheDatabaseAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.Inventory!.Money = 500_000;

        ResetViews(player);
        var changed = await PlayerInventoryRefresh.ApplyAsync(player, State(400_000)).ConfigureAwait(false);

        Assert.That(changed, Is.True);
        Assert.That(player.SelectedCharacter.Inventory.Money, Is.EqualTo(400_000));
        Mock.Get(player.ViewPlugIns.GetPlugIn<IUpdateMoneyPlugIn>()!).Verify(v => v.UpdateMoneyAsync(), Times.Once);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IUpdateInventoryListPlugIn>()!).Verify(v => v.UpdateInventoryListAsync(), Times.Never);
    }

    /// <summary>
    /// Nothing changed in the database: nothing changes live and the client is not bothered.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UnchangedStateTouchesNothingAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var item = NewItem(FirstSlot);
        await player.Inventory!.AddItemAsync(FirstSlot, item).ConfigureAwait(false);
        player.SelectedCharacter!.Inventory!.Money = 10;

        ResetViews(player);
        var changed = await PlayerInventoryRefresh.ApplyAsync(player, State(10, item)).ConfigureAwait(false);

        Assert.That(changed, Is.False);
        Assert.That(player.Inventory.GetItem(FirstSlot), Is.SameAs(item));
        Mock.Get(player.ViewPlugIns.GetPlugIn<IUpdateInventoryListPlugIn>()!).Verify(v => v.UpdateInventoryListAsync(), Times.Never);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IUpdateMoneyPlugIn>()!).Verify(v => v.UpdateMoneyAsync(), Times.Never);
    }

    /// <summary>
    /// Worn items are never compared: the Auction House does not change them.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task EquippedItemsAreLeftAloneAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var worn = NewItem(3);
        await player.Inventory!.AddItemAsync(3, worn).ConfigureAwait(false);

        await PlayerInventoryRefresh.ApplyAsync(player, State(0)).ConfigureAwait(false);

        Assert.That(player.Inventory.GetItem(3), Is.SameAs(worn));
    }

    private static void ResetViews(Player player)
    {
        // Entering the world already sent the inventory and the Zen once.
        Mock.Get(player.ViewPlugIns.GetPlugIn<IUpdateInventoryListPlugIn>()!).Invocations.Clear();
        Mock.Get(player.ViewPlugIns.GetPlugIn<IUpdateMoneyPlugIn>()!).Invocations.Clear();
    }

    private static AuctionInventoryState State(long money, params Item[] items)
        => new(money, items.Select(i => new AuctionInventoryItemState(((BasicItem)i).Id, i.ItemSlot, i.Durability, i.Level)).ToList());

    private static Item NewItem(int slot, double durability = 1)
    {
        var item = new BasicItem { Id = Guid.NewGuid(), ItemSlot = (byte)slot, Durability = durability };
        item.Definition = new ItemDefinition { Width = 1, Height = 1 };
        return item;
    }
}
