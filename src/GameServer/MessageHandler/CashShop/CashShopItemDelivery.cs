// <copyright file="CashShopItemDelivery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.CashShop;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Persistence.CashShop;

/// <summary>Turns a stored Cash Shop entry into a real item in the player's inventory.</summary>
internal static class CashShopItemDelivery
{
    /// <summary>Checks that the item exists in the game configuration of the player.</summary>
    /// <param name="player">The player.</param>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="level">The item level.</param>
    /// <returns>True when the item can be created.</returns>
    internal static bool IsDeliverable(Player player, byte group, short number, byte level)
    {
        var definition = player.GameContext.Configuration.Items.FirstOrDefault(i => i.Group == group && i.Number == number);
        return definition is not null && level <= definition.MaximumItemLevel;
    }

    /// <summary>Creates the item and puts it into the first free inventory spot.</summary>
    /// <param name="player">The player.</param>
    /// <param name="stored">The stored entry.</param>
    /// <returns>True when the item was added; false when the inventory has no room (nothing was created).</returns>
    internal static async ValueTask<bool> TryDeliverAsync(Player player, CashShopStorageItem stored)
    {
        var definition = player.GameContext.Configuration.Items.FirstOrDefault(i => i.Group == stored.ItemGroup && i.Number == stored.ItemNumber);
        if (definition is null || player.Inventory is null)
        {
            return false;
        }

        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Level = stored.ItemLevel;
        if (stored.RentalDurationSeconds is > 0)
        {
            item.RentalExpiresAt = DateTimeOffset.UtcNow.AddSeconds(stored.RentalDurationSeconds.Value);
        }
        item.Durability = item.IsStackable() ? stored.Quantity : definition.Durability;

        bool added;
        try
        {
            added = await player.Inventory.AddItemAsync(item).ConfigureAwait(false);
        }
        catch
        {
            if (player.Inventory.Items.Contains(item))
            {
                // The grant went through although something after it failed: the item is the player's now, the storage entry must not come back.
                added = true;
            }
            else
            {
                await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
                throw;
            }
        }

        if (!added)
        {
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            return false;
        }

        // From here on the item belongs to the player. Neither a failing client notification nor a failing save may report "not delivered":
        // the caller would put the storage entry back, and the player would get the item twice.
        try
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(plugIn => plugIn.ItemAppearAsync(item)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "The Cash Shop item was delivered to {Player}, but the client could not be notified.", player);
        }

        // The storage entry is already deleted for good. Save the new item right away, so that it does not exist only in memory until the
        // next periodic save (a crash in between would lose a paid item).
        try
        {
            await player.SaveProgressAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "The Cash Shop item was delivered to {Player}, but the character could not be saved right away.", player);
        }

        return true;
    }
}
