// <copyright file="JewelBankPickup.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.JewelBank;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.Views.JewelBank;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// Sends picked-up jewels and fruits straight into the Jewel Bank when the account has switched auto-deposit on.
/// </summary>
public static class JewelBankPickup
{
    /// <summary>
    /// Finds the Jewel Bank kind of an item on the floor, if the bank would take it: a plain jewel or a fruit, without options or sockets.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The kind, or <c>null</c> when the item is not bankable.</returns>
    public static JewelBankKind? FindKind(Item item)
    {
        if (item.Definition is not { } definition || item.Durability != 1 || item.SocketCount > 0 || (item.ItemOptions?.Count ?? 0) > 0)
        {
            return null;
        }

        return JewelBankKinds.Find(definition.Group, definition.Number, item.Level);
    }

    /// <summary>
    /// Tries to bank a dropped item for the player. The drop is claimed and credited as one step, so it can end up
    /// either in the bank or on the floor (or somebody else's inventory), never both and never nowhere.
    /// </summary>
    /// <param name="player">The player who picks the item up.</param>
    /// <param name="droppedItem">The dropped item.</param>
    /// <returns><c>true</c> when the item went into the bank; <c>false</c> when the normal pickup should go on.</returns>
    public static async ValueTask<bool> TryDepositAsync(Player player, DroppedItem droppedItem)
    {
        try
        {
            if (FindKind(droppedItem.Item) is not { } kind
                || JewelBankService.GetIdentity(player) is not { } who
                || JewelBankService.Create(player) is not { } service
                || !await service.IsAutoDepositEnabledAsync(who.AccountId).ConfigureAwait(false))
            {
                return false;
            }

            long? newCount = null;
            var banked = await droppedItem.TryPickUpIntoBankAsync(
                player,
                async () =>
                {
                    newCount = await service.CreditPickupAsync(who.AccountId, who.CharacterId, kind.Code, 1).ConfigureAwait(false);
                    return newCount is not null;
                }).ConfigureAwait(false);
            if (!banked || newCount is not { } count)
            {
                return false;
            }

            await player.InvokeViewPlugInAsync<IJewelBankPickupPlugIn>(p => p.JewelBankPickupAsync(kind.Code, 1, (ulong)count)).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            // The item stays on the floor and the normal pickup takes over; a database problem must never destroy a drop.
            player.Logger.LogError(ex, "The Jewel Bank could not take a picked-up item for {Player}; the item is picked up normally.", player);
            return false;
        }
    }
}
