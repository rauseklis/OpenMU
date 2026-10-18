// <copyright file="PlayerInventoryRefresh.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Brings the in-memory inventory and Zen of a live player up to date with the committed database state, after the
/// Auction House changed them with its own transactions. This replaces the former disconnect of the player.
/// </summary>
/// <remarks>
/// The Auction House moves items between a character's inventory and its listings and mailbox, and charges or pays Zen,
/// with SQL of its own. The player's persistence context never sees those changes, so the live copy must not simply
/// stay as it is: a later save would act on stale entities. Items which are gone are detached from the player's context
/// (not deleted, they now belong to a listing), items which arrived are loaded into it, and the Zen is replaced by the
/// committed value. Only the inventory slots after the equipment are compared; the Auction House never touches worn items.
/// </remarks>
public static class PlayerInventoryRefresh
{
    /// <summary>
    /// Applies the committed state to the live player and tells the client about the changes.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="state">The committed inventory state.</param>
    /// <param name="loadItem">Loads an item into the persistence context of the player; the default loads it from the context.</param>
    /// <returns><c>true</c> when something had to change.</returns>
    public static async ValueTask<bool> ApplyAsync(Player player, AuctionInventoryState state, Func<Guid, Task<Item?>>? loadItem = null)
    {
        if (player.Inventory is not { } inventory || player.SelectedCharacter is not { } character)
        {
            return false;
        }

        loadItem ??= id => player.PersistenceContext.GetByIdAsync<Item>(id);
        var firstSlot = InventoryConstants.EquippableSlotsCount;
        var committed = state.Items.Where(i => i.Slot >= firstSlot).ToDictionary(i => i.Id);
        var kept = new HashSet<Guid>();
        var changed = false;

        foreach (var item in inventory.Items.Where(i => i.ItemSlot >= firstSlot).ToList())
        {
            if (IdOf(item) is var id && committed.TryGetValue(id, out var row) && IsSame(item, row))
            {
                kept.Add(id);
                continue;
            }

            // Gone from the inventory (it belongs to a listing now, or it was consumed): forget it without deleting it.
            player.PersistenceContext.Detach(item);
            await inventory.RemoveItemAsync(item).ConfigureAwait(false);
            changed = true;
        }

        foreach (var row in committed.Values.Where(r => !kept.Contains(r.Id)).OrderBy(r => r.Slot))
        {
            var item = await loadItem(row.Id).ConfigureAwait(false);
            if (item is null)
            {
                player.Logger.LogWarning("Auction House refresh: item {ItemId} of the committed inventory could not be loaded.", row.Id);
                continue;
            }

            if (await inventory.AddItemAsync(row.Slot, item).ConfigureAwait(false))
            {
                changed = true;
            }
            else
            {
                player.PersistenceContext.Detach(item);
                player.Logger.LogWarning("Auction House refresh: item {ItemId} does not fit into slot {Slot}.", row.Id, row.Slot);
            }
        }

        var moneyChanged = false;
        if (character.Inventory is { } characterStorage && characterStorage.Money != state.Money)
        {
            characterStorage.Money = state.Money;
            moneyChanged = true;
        }

        if (changed)
        {
            await player.InvokeViewPlugInAsync<IUpdateInventoryListPlugIn>(p => p.UpdateInventoryListAsync()).ConfigureAwait(false);
        }

        if (moneyChanged)
        {
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        return changed || moneyChanged;
    }

    private static Guid IdOf(Item item) => item is IIdentifiable identifiable ? identifiable.Id : Guid.Empty;

    private static bool IsSame(Item item, AuctionInventoryItemState row)
        => item.ItemSlot == row.Slot && item.Level == row.Level && Math.Abs(item.Durability - row.Durability) < 0.0001;
}
