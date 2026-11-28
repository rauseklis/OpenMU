// <copyright file="DarkInvasionSpawnPoints.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;

using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Finds the tiles on which the Dark Skeleton invasion places its monsters: tiles which players can
/// walk to from a start area (the town, or the entrance of a map like Kalima), outside of the safezone,
/// and at least a minimum distance away from that start area.
/// </summary>
internal static class DarkInvasionSpawnPoints
{
    private const int MapSize = 256;

    /// <summary>
    /// Gets the safezone tiles of the terrain.
    /// </summary>
    /// <param name="terrain">The terrain.</param>
    /// <returns>The safezone tiles.</returns>
    public static List<Point> GetSafezoneTiles(GameMapTerrain terrain)
    {
        var result = new List<Point>();
        for (var x = 0; x < MapSize; x++)
        {
            for (var y = 0; y < MapSize; y++)
            {
                if (terrain.SafezoneMap[x, y])
                {
                    result.Add(new Point((byte)x, (byte)y));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the tiles of the gates at which players arrive on the map (e.g. the Kalima entrance).
    /// </summary>
    /// <param name="mapDefinition">The map definition.</param>
    /// <returns>The tiles of the arrival gates.</returns>
    public static List<Point> GetEntranceTiles(GameMapDefinition mapDefinition)
    {
        var result = new List<Point>();
        foreach (var gate in mapDefinition.ExitGates)
        {
            // The counters are ints on purpose: a gate reaching to coordinate 255 would make a byte counter wrap around.
            for (int x = Math.Min(gate.X1, gate.X2); x <= Math.Max(gate.X1, gate.X2); x++)
            {
                for (int y = Math.Min(gate.Y1, gate.Y2); y <= Math.Max(gate.Y1, gate.Y2); y++)
                {
                    result.Add(new Point((byte)x, (byte)y));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Finds the walkable tiles outside the safezone which can be reached on foot (in all eight directions)
    /// from one of the start tiles, and which are at least <paramref name="minimumDistance"/> tiles away from
    /// every start tile (counted straight, through walls).
    /// </summary>
    /// <param name="terrain">The terrain.</param>
    /// <param name="startTiles">The start tiles.</param>
    /// <param name="minimumDistance">The minimum distance in tiles.</param>
    /// <returns>The suitable tiles; empty if there is none.</returns>
    public static Point[] Find(GameMapTerrain terrain, IReadOnlyCollection<Point> startTiles, byte minimumDistance)
    {
        if (startTiles.Count == 0)
        {
            return [];
        }

        var distance = minimumDistance > 0 ? GetDistances(startTiles) : null;
        var reached = new bool[MapSize, MapSize];
        var queue = new Queue<Point>();
        foreach (var tile in startTiles)
        {
            if (terrain.WalkMap[tile.X, tile.Y] && !reached[tile.X, tile.Y])
            {
                reached[tile.X, tile.Y] = true;
                queue.Enqueue(tile);
            }
        }

        var result = new List<Point>();
        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            if (!terrain.SafezoneMap[point.X, point.Y]
                && (distance is null || distance[point.X, point.Y] >= minimumDistance))
            {
                result.Add(point);
            }

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var x = point.X + dx;
                    var y = point.Y + dy;
                    if (x is < 0 or >= MapSize || y is < 0 or >= MapSize || reached[x, y] || !terrain.WalkMap[x, y])
                    {
                        continue;
                    }

                    reached[x, y] = true;
                    queue.Enqueue(new Point((byte)x, (byte)y));
                }
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Gets the straight distance (the larger of the x and y difference) of every tile to the nearest start tile.
    /// </summary>
    /// <param name="startTiles">The start tiles.</param>
    /// <returns>The distance per tile.</returns>
    private static int[,] GetDistances(IReadOnlyCollection<Point> startTiles)
    {
        var distance = new int[MapSize, MapSize];
        for (var x = 0; x < MapSize; x++)
        {
            for (var y = 0; y < MapSize; y++)
            {
                distance[x, y] = -1;
            }
        }

        var queue = new Queue<Point>();
        foreach (var tile in startTiles)
        {
            if (distance[tile.X, tile.Y] < 0)
            {
                distance[tile.X, tile.Y] = 0;
                queue.Enqueue(tile);
            }
        }

        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            var next = distance[point.X, point.Y] + 1;
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var x = point.X + dx;
                    var y = point.Y + dy;
                    if (x is < 0 or >= MapSize || y is < 0 or >= MapSize || distance[x, y] >= 0)
                    {
                        continue;
                    }

                    distance[x, y] = next;
                    queue.Enqueue(new Point((byte)x, (byte)y));
                }
            }
        }

        return distance;
    }
}
