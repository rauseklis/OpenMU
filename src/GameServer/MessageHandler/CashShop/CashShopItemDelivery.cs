// <copyright file="CashShopItemDelivery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.CashShop;

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
        if (await player.Inventory.AddItemAsync(item).ConfigureAwait(false))
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(plugIn => plugIn.ItemAppearAsync(item)).ConfigureAwait(false);
            return true;
        }

        await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
        return false;
    }
}
