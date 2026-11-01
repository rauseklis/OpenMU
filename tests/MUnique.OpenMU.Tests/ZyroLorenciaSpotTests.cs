// <copyright file="ZyroLorenciaSpotTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Verifies that only Zyro's Lorencia spawn point is moved in an existing configuration.
/// </summary>
[TestFixture]
public class ZyroLorenciaSpotTests
{
    private const short ZyroNumber = 568;
    private const short LorenciaNumber = 0;
    private const short DeviasNumber = 2;

    /// <summary>
    /// Zyro's Lorencia spawn point moves to 124/144, while his Devias spawn point and the other Lorencia spawns stay.
    /// </summary>
    [Test]
    public async Task UpdateMovesOnlyZyrosLorenciaSpawn()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>();
        var zyro = context.CreateNew<MonsterDefinition>();
        zyro.Number = ZyroNumber;
        var other = context.CreateNew<MonsterDefinition>();
        other.Number = 371;
        var zyroInLorencia = AddSpawn(context, configuration, LorenciaNumber, zyro, 131, 139);
        var otherInLorencia = AddSpawn(context, configuration, LorenciaNumber, other, 130, 126);
        var zyroInDevias = AddSpawn(context, configuration, DeviasNumber, zyro, 225, 52);

        await new ZyroLorenciaSpotUpdatePlugIn().ApplyUpdateAsync(context, configuration);

        Assert.That((zyroInLorencia.X1, zyroInLorencia.X2, zyroInLorencia.Y1, zyroInLorencia.Y2), Is.EqualTo(((byte)124, (byte)124, (byte)144, (byte)144)));
        Assert.That((otherInLorencia.X1, otherInLorencia.Y1), Is.EqualTo(((byte)130, (byte)126)));
        Assert.That((zyroInDevias.X1, zyroInDevias.Y1), Is.EqualTo(((byte)225, (byte)52)));
    }

    private static MonsterSpawnArea AddSpawn(IContext context, GameConfiguration configuration, short mapNumber, MonsterDefinition monster, byte x, byte y)
    {
        var map = configuration.Maps.FirstOrDefault(m => m.Number == mapNumber);
        if (map is null)
        {
            map = context.CreateNew<GameMapDefinition>();
            map.Number = mapNumber;
            configuration.Maps.Add(map);
        }

        var spawn = context.CreateNew<MonsterSpawnArea>();
        spawn.GameMap = map;
        spawn.MonsterDefinition = monster;
        spawn.X1 = x;
        spawn.X2 = x;
        spawn.Y1 = y;
        spawn.Y2 = y;
        map.MonsterSpawns.Add(spawn);
        return spawn;
    }
}
