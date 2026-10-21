// <copyright file="JewelBankInventory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.JewelBank;

using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// Counts what a player carries which the Jewel Bank would take, by kind, from the live inventory. It applies the same rules as a deposit:
/// plain items of durability 1 without options or sockets; every item is one unit of its kind, a bundle included.
/// </summary>
public static class JewelBankInventory
{
    /// <summary>
    /// Counts the items of each kind the player carries in the inventory (not the equipment).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The items by kind code; kinds the player does not carry are missing.</returns>
    public static IReadOnlyDictionary<byte, long> Count(Player player)
    {
        var counts = new Dictionary<byte, long>();
        if (player.Inventory?.Items is not { } items)
        {
            return counts;
        }

        foreach (var item in items)
        {
            if (item.ItemSlot < InventoryConstants.EquippableSlotsCount
                || item.Definition is not { } definition
                || item.Durability != 1
                || item.SocketCount > 0
                || (item.ItemOptions?.Count ?? 0) > 0)
            {
                continue;
            }

            if (JewelBankKinds.Find(definition.Group, definition.Number, item.Level) is { } kind)
            {
                counts[kind.Code] = counts.GetValueOrDefault(kind.Code) + 1;
            }
        }

        return counts;
    }
}
