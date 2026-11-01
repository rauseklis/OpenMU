// <copyright file="ZyroLorenciaSpotUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;
using MUnique.OpenMU.PlugIns;

/// <summary>Moves the Lorencia spawn point of Wandering Merchant Zyro in existing Season Six configurations.</summary>
[PlugIn]
[Guid("A86B142D-53E8-426D-82FA-EF3604A6554F")]
public sealed class ZyroLorenciaSpotUpdatePlugIn : UpdatePlugInBase
{
    private const short ZyroNumber = 568;

    /// <inheritdoc />
    public override string Name => "Move Zyro's Lorencia spawn point";

    /// <inheritdoc />
    public override string Description => "Moves Wandering Merchant Zyro's Lorencia spawn point from 131/139 to 124/144; his other maps are not touched.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ZyroLorenciaSpot;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var lorencia = gameConfiguration.Maps.Single(map => map.Number == Lorencia.Number);
        var spawn = lorencia.MonsterSpawns.Single(area => area.MonsterDefinition?.Number == ZyroNumber);
        spawn.X1 = Lorencia.ZyroX;
        spawn.X2 = Lorencia.ZyroX;
        spawn.Y1 = Lorencia.ZyroY;
        spawn.Y2 = Lorencia.ZyroY;
        return ValueTask.CompletedTask;
    }
}
