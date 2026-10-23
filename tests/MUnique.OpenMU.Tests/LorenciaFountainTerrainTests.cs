// <copyright file="LorenciaFountainTerrainTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using Basic = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Verifies that Lorencia blocks the tiles of the Loren Market fountain which replaced its old fountain.
/// </summary>
[TestFixture]
public class LorenciaFountainTerrainTests
{
    private const byte Safezone = 1;
    private const byte SafezoneBlocked = 5;

    /// <summary>
    /// The tiles the new fountain covers, copied from the Loren Market fountain (centred on 128,128)
    /// and moved to the old fountain's centre at 141,128.
    /// </summary>
    private static readonly (byte X, byte Y)[] BlockedTiles =
    {
        (140, 125), (141, 125),
        (139, 126), (140, 126), (141, 126), (142, 126),
        (138, 127), (139, 127), (140, 127), (141, 127), (142, 127), (143, 127),
        (138, 128), (139, 128), (140, 128), (141, 128), (142, 128), (143, 128),
        (138, 129), (139, 129), (140, 129), (141, 129), (142, 129),
        (139, 130), (140, 130), (141, 130),
    };

    /// <summary>
    /// Tests that a database created with the shipped Lorencia terrain blocks the fountain and nothing around it.
    /// </summary>
    [Test]
    public async Task UpdatedTerrainBlocksTheFountainTiles()
    {
        var terrain = await this.ApplyUpdateAsync();

        foreach (var (x, y) in BlockedTiles)
        {
            Assert.That(terrain.AttributeMap[x, y], Is.EqualTo(SafezoneBlocked), $"tile {x},{y} must be blocked");
            Assert.That(terrain.WalkMap[x, y], Is.False, $"tile {x},{y} must not be walkable");
        }

        Assert.That(terrain.AttributeMap[144, 128], Is.EqualTo(Safezone), "the square around the fountain stays walkable");
        Assert.That(terrain.AttributeMap[141, 131], Is.EqualTo(Safezone));
        Assert.That(terrain.AttributeMap[141, 124], Is.EqualTo(Safezone));
    }

    /// <summary>
    /// Tests that the update plug-in gives an existing database the new terrain.
    /// </summary>
    [Test]
    public async Task UpdatePlugInReplacesTheTerrainOfAnExistingDatabase()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<Basic.GameConfiguration>();
        var map = context.CreateNew<Basic.GameMapDefinition>();
        map.Number = 0;
        map.Name = "Lorencia";
        map.TerrainData = new byte[3 * ushort.MaxValue];
        configuration.Maps.Add(map);

        await new LorenciaMarketFountainUpdatePlugIn().ApplyUpdateAsync(context, configuration);

        Assert.That(new GameMapTerrain(map.TerrainData).WalkMap[141, 128], Is.False);
    }

    /// <summary>
    /// Tests that the two grass planters east of the fountain are gone, so their tiles can be walked on.
    /// </summary>
    [Test]
    public async Task UpdatedTerrainFreesTheRemovedPlanterTiles()
    {
        var terrain = await this.ApplyUpdateAsync();

        for (byte x = 148; x <= 151; x++)
        {
            foreach (var y in new byte[] { 124, 125, 126, 130, 131, 132 })
            {
                Assert.That(terrain.WalkMap[x, y], Is.True, $"tile {x},{y} must be walkable");
            }
        }
    }

    /// <summary>
    /// Tests that the two Loren Market lanterns west of the fountain block only the tile they stand on.
    /// </summary>
    [Test]
    public async Task UpdatedTerrainBlocksOnlyTheLanternTiles()
    {
        var terrain = await this.ApplyUpdateAsync();

        foreach (var (x, y) in new (byte X, byte Y)[] { (130, 121), (130, 134) })
        {
            Assert.That(terrain.WalkMap[x, y], Is.False, $"tile {x},{y} must be blocked");
        }

        foreach (var (x, y) in new (byte X, byte Y)[] { (131, 120), (134, 134), (134, 121), (135, 122), (135, 127), (133, 131), (134, 128) })
        {
            Assert.That(terrain.WalkMap[x, y], Is.True, $"tile {x},{y} must be walkable");
        }
    }

    /// <summary>
    /// Tests that the four Loren Market lantern planters around the fountain block their tiles, while the
    /// ground between them and the fountain, so that a player can walk around it, stays walkable.
    /// </summary>
    [Test]
    public async Task UpdatedTerrainBlocksTheFourPlantersAroundTheFountain()
    {
        var terrain = await this.ApplyUpdateAsync();

        foreach (var (x, y) in new (byte X, byte Y)[] { (137, 124), (137, 131), (144, 124), (144, 131) })
        {
            Assert.That(terrain.WalkMap[x, y], Is.False, $"tile {x},{y} must be blocked");
        }

        foreach (var (x, y) in new (byte X, byte Y)[] { (141, 121), (141, 135), (134, 128), (148, 128), (139, 125), (138, 126), (142, 125), (143, 126), (138, 130), (143, 130) })
        {
            Assert.That(terrain.WalkMap[x, y], Is.True, $"tile {x},{y} must be walkable");
        }
    }

    private async Task<GameMapTerrain> ApplyUpdateAsync()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<Basic.GameConfiguration>();
        var map = context.CreateNew<Basic.GameMapDefinition>();
        map.Number = 0;
        map.Name = "Lorencia";
        map.TerrainData = new byte[3 * ushort.MaxValue];
        configuration.Maps.Add(map);
        await new LorenciaMarketFountainUpdatePlugIn().ApplyUpdateAsync(context, configuration);
        return new GameMapTerrain(map.TerrainData);
    }
}
