// <copyright file="JewelRainDropHelperTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.JewelRain;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.JewelRain;

/// <summary>
/// Tests the <see cref="JewelRainDropHelper"/>.
/// </summary>
[TestFixture]
public class JewelRainDropHelperTest
{
    private const byte Blocked = 4;

    private const byte Safezone = 1;

    /// <summary>
    /// Only walkable cells inside the rectangle are picked; safezone cells are allowed.
    /// </summary>
    [Test]
    public void PicksOnlyWalkableCellsInsideRectangle()
    {
        var terrain = CreateTerrain(walkableFrom: (10, 20), walkableTo: (12, 22), walkableValue: Safezone);
        var config = new JewelRainConfiguration { AreaX1 = 0, AreaY1 = 0, AreaX2 = 255, AreaY2 = 255 };

        for (var i = 0; i < 100; i++)
        {
            var cell = JewelRainDropHelper.TryGetDropCell(terrain, config);
            Assert.That(cell, Is.Not.Null);
            Assert.That(cell!.Value.X, Is.InRange(10, 12));
            Assert.That(cell.Value.Y, Is.InRange(20, 22));
        }
    }

    /// <summary>
    /// The rectangle may be given with swapped corners and still limits the cells.
    /// </summary>
    [Test]
    public void SwappedCornersStillLimitTheRectangle()
    {
        var terrain = CreateTerrain(walkableFrom: (10, 20), walkableTo: (50, 60), walkableValue: 0);
        var config = new JewelRainConfiguration { AreaX1 = 12, AreaY1 = 22, AreaX2 = 10, AreaY2 = 20 };

        for (var i = 0; i < 100; i++)
        {
            var cell = JewelRainDropHelper.TryGetDropCell(terrain, config);
            Assert.That(cell!.Value.X, Is.InRange(10, 12));
            Assert.That(cell.Value.Y, Is.InRange(20, 22));
        }
    }

    /// <summary>
    /// A rectangle without any walkable cell yields no drop cell.
    /// </summary>
    [Test]
    public void ReturnsNullWhenRectangleHasNoWalkableCell()
    {
        var terrain = CreateTerrain(walkableFrom: (10, 20), walkableTo: (12, 22), walkableValue: 0);
        var config = new JewelRainConfiguration { AreaX1 = 100, AreaY1 = 100, AreaX2 = 120, AreaY2 = 120 };

        Assert.That(JewelRainDropHelper.TryGetDropCell(terrain, config), Is.Null);
    }

    /// <summary>
    /// An item which is not part of the game configuration cannot be created.
    /// </summary>
    [Test]
    public async ValueTask UnknownItemReturnsNullAsync()
    {
        var context = GameContextTestHelper.CreateGameContext();
        try
        {
            Assert.That(JewelRainDropHelper.TryCreateItem(context, new JewelRainStep(14, 15, 0, 15)), Is.Null);
        }
        finally
        {
            await ((GameContext)context).DisposeAsync().ConfigureAwait(false);
        }
    }

    private static GameMapTerrain CreateTerrain((int X, int Y) walkableFrom, (int X, int Y) walkableTo, byte walkableValue)
    {
        var data = new byte[3 + (256 * 256)];
        Array.Fill(data, Blocked, 3, 256 * 256);
        for (var x = walkableFrom.X; x <= walkableTo.X; x++)
        {
            for (var y = walkableFrom.Y; y <= walkableTo.Y; y++)
            {
                data[3 + (y * 256) + x] = walkableValue;
            }
        }

        return new GameMapTerrain(data);
    }
}
