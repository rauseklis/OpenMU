// <copyright file="JewelRainDropHelper.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.JewelRain;

using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Helper methods to find drop positions and to create the items of the Jewel Rain.
/// </summary>
public static class JewelRainDropHelper
{
    /// <summary>
    /// Gets a random walkable cell inside the configured drop area. Safezone cells are allowed.
    /// </summary>
    /// <param name="terrain">The terrain of the map.</param>
    /// <param name="config">The configuration which contains the drop area.</param>
    /// <returns>A random walkable cell, or <c>null</c> if the area has none.</returns>
    public static Point? TryGetDropCell(GameMapTerrain terrain, JewelRainConfiguration config)
    {
        // The bounds are ints on purpose: a byte counter would wrap around at 255 and loop forever.
        int minX = Math.Min(config.AreaX1, config.AreaX2);
        int maxX = Math.Max(config.AreaX1, config.AreaX2);
        int minY = Math.Min(config.AreaY1, config.AreaY2);
        int maxY = Math.Max(config.AreaY1, config.AreaY2);

        var cells = new List<Point>();
        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                if (terrain.WalkMap[x, y])
                {
                    cells.Add(new Point((byte)x, (byte)y));
                }
            }
        }

        return cells.Count == 0 ? null : cells[Random.Shared.Next(cells.Count)];
    }

    /// <summary>
    /// Creates a temporary item for the given step.
    /// </summary>
    /// <param name="context">The game context which holds the item definitions.</param>
    /// <param name="step">The step.</param>
    /// <returns>The item, or <c>null</c> if the item definition does not exist.</returns>
    public static Item? TryCreateItem(IGameContext context, JewelRainStep step)
    {
        var definition = context.Configuration.Items.FirstOrDefault(d => d.Group == step.Group && d.Number == step.Number);
        if (definition is null)
        {
            return null;
        }

        var item = new TemporaryItem
        {
            Definition = definition,
            Level = step.Level,
        };
        item.Durability = item.IsStackable() ? 1 : definition.Durability;
        return item;
    }
}
