// <copyright file="LorenciaMarketFountainUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Gives Lorencia the terrain with the blocked tiles of the Loren Market fountain, which replaced the old fountain
/// in the town square. The client shows the new fountain from the same change.
/// </summary>
[PlugIn]
[Guid("8E2F6B14-5A39-4C7D-B1E0-3D94A7C25F68")]
public sealed class LorenciaMarketFountainUpdatePlugIn : UpdatePlugInBase
{
    private const short LorenciaMapNumber = 0;

    /// <inheritdoc />
    public override string Name => "Lorencia market fountain";

    /// <inheritdoc />
    public override string Description => "Replaces the Lorencia terrain so the tiles of the Loren Market fountain, which replaced the old fountain in the town square, are blocked.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.LorenciaMarketFountain;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var lorencia = gameConfiguration.Maps.SingleOrDefault(map => map.Number == LorenciaMapNumber);
        lorencia?.UpdateTerrainFromResources();
        return ValueTask.CompletedTask;
    }
}
