// <copyright file="InventoryPlacement.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

using MUnique.OpenMU.DataModel;

/// <summary>
/// Finds room for items in the main inventory grid, as the game itself lays it out: eight columns, eight rows, the first
/// slot after the equipment, every item covering as many cells as its width and height say.
/// </summary>
/// <remarks>
/// The Auction House puts items into inventories with SQL, so it has to do the fitting itself. Looking only at which slot
/// numbers are taken is not enough: a large item covers cells whose slot numbers no item names, and an item which is pushed
/// into one of them overlaps the large one.
/// </remarks>
public static class InventoryPlacement
{
    private static int Columns => InventoryConstants.RowSize;

    private static int Rows => InventoryConstants.InventoryRows;

    private static int FirstSlot => InventoryConstants.EquippableSlotsCount;

    /// <summary>
    /// Finds a slot for each of the new items, one after the other, each at the first position (row by row) where it fits.
    /// </summary>
    /// <param name="occupied">The items already in the inventory: their slot, width and height. Worn items are ignored.</param>
    /// <param name="newItems">The width and height of each item which needs a place.</param>
    /// <returns>The slot of each new item in the order given, or <c>null</c> when they do not all fit.</returns>
    public static IReadOnlyList<int>? Place(IEnumerable<(int Slot, int Width, int Height)> occupied, IReadOnlyList<(int Width, int Height)> newItems)
    {
        var used = new bool[Rows, Columns];
        foreach (var (slot, width, height) in occupied)
        {
            if (slot < FirstSlot)
            {
                continue;
            }

            Mark(used, slot - FirstSlot, Math.Max(width, 1), Math.Max(height, 1));
        }

        var slots = new List<int>(newItems.Count);
        foreach (var (width, height) in newItems)
        {
            var found = FindFree(used, Math.Max(width, 1), Math.Max(height, 1));
            if (found is not { } index)
            {
                return null;
            }

            Mark(used, index, Math.Max(width, 1), Math.Max(height, 1));
            slots.Add(FirstSlot + index);
        }

        return slots;
    }

    private static int? FindFree(bool[,] used, int width, int height)
    {
        for (var row = 0; row + height <= Rows; row++)
        {
            for (var column = 0; column + width <= Columns; column++)
            {
                if (Fits(used, row, column, width, height))
                {
                    return (row * Columns) + column;
                }
            }
        }

        return null;
    }

    private static bool Fits(bool[,] used, int row, int column, int width, int height)
    {
        for (var r = row; r < row + height; r++)
        {
            for (var c = column; c < column + width; c++)
            {
                if (used[r, c])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void Mark(bool[,] used, int index, int width, int height)
    {
        var row = index / Columns;
        var column = index % Columns;
        for (var r = row; r < row + height && r < Rows; r++)
        {
            for (var c = column; c < column + width && c < Columns; c++)
            {
                used[r, c] = true;
            }
        }
    }
}
