// <copyright file="InventoryPlacementTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// Tests of <see cref="InventoryPlacement"/>: items which come out of the Auction House must land where they fit, large ones
/// included, and must never overlap what is already in the inventory.
/// </summary>
[TestFixture]
public class InventoryPlacementTests
{
    private static readonly int First = InventoryConstants.EquippableSlotsCount;

    /// <summary>
    /// An empty inventory takes the first slot.
    /// </summary>
    [Test]
    public void EmptyInventoryUsesTheFirstSlot()
    {
        var slots = InventoryPlacement.Place([], [(1, 1)]);

        Assert.That(slots, Is.EqualTo(new[] { First }));
    }

    /// <summary>
    /// A small item skips every cell which a large item covers, also the ones no item names.
    /// </summary>
    [Test]
    public void SmallItemAvoidsTheCellsOfALargeItem()
    {
        // a 2 x 4 item in the top left corner covers the columns 0 and 1 of the rows 0 to 3.
        var slots = InventoryPlacement.Place([(First, 2, 4)], [(1, 1)]);

        Assert.That(slots, Is.EqualTo(new[] { First + 2 }));
    }

    /// <summary>
    /// A large item moves past a small item in its way.
    /// </summary>
    [Test]
    public void LargeItemMovesPastASmallItemInItsWay()
    {
        // a 1 x 1 item in row 1, column 1 blocks every 2 x 4 position which starts in the columns 0 and 1.
        var slots = InventoryPlacement.Place([(First + 9, 1, 1)], [(2, 4)]);

        Assert.That(slots, Is.EqualTo(new[] { First + 2 }));
    }

    /// <summary>
    /// An item which would stick out of the right edge does not fit there.
    /// </summary>
    [Test]
    public void WideItemDoesNotStickOutOfTheRightEdge()
    {
        // only the last column is free.
        var occupied = new List<(int, int, int)>();
        for (var row = 0; row < InventoryConstants.InventoryRows; row++)
        {
            occupied.Add((First + (row * InventoryConstants.RowSize), 7, 1));
        }

        Assert.That(InventoryPlacement.Place(occupied, [(2, 1)]), Is.Null);
        Assert.That(InventoryPlacement.Place(occupied, [(1, 1)]), Is.EqualTo(new[] { First + 7 }));
    }

    /// <summary>
    /// A full inventory takes nothing.
    /// </summary>
    [Test]
    public void FullInventoryTakesNothing()
    {
        var occupied = Enumerable.Range(0, InventoryConstants.InventoryRows * InventoryConstants.RowSize)
            .Select(i => (First + i, 1, 1))
            .ToList();

        Assert.That(InventoryPlacement.Place(occupied, [(1, 1)]), Is.Null);
    }

    /// <summary>
    /// Several new items never overlap each other, and either all of them are placed or none is.
    /// </summary>
    [Test]
    public void SeveralNewItemsGetSeparatePlacesOrNone()
    {
        var slots = InventoryPlacement.Place([], [(2, 2), (2, 2), (2, 2)]);

        Assert.That(slots, Is.EqualTo(new[] { First, First + 2, First + 4 }));

        // five 4 x 4 items cannot fit into 8 x 8 cells: not a single one is placed.
        Assert.That(InventoryPlacement.Place([], Enumerable.Repeat((4, 4), 5).ToList()), Is.Null);
    }

    /// <summary>
    /// Worn items are not part of the grid.
    /// </summary>
    [Test]
    public void WornItemsAreIgnored()
    {
        var slots = InventoryPlacement.Place([(3, 2, 4)], [(1, 1)]);

        Assert.That(slots, Is.EqualTo(new[] { First }));
    }
}
