// <copyright file="CrustRespawnDelayUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Gives the Mega Crust and the Alpha Crust the respawn delay of the Alquamos (10 seconds) instead of 3 seconds.
/// </summary>
[PlugIn]
[Guid("8E2F5B71-4C93-4D0A-A6B8-1F7E3C9D52A4")]
public sealed class CrustRespawnDelayUpdatePlugIn : UpdatePlugInBase
{
    private const short MegaCrustNumber = 71;
    private const short AlphaCrustNumber = 74;
    private static readonly TimeSpan RespawnDelay = TimeSpan.FromSeconds(10);

    public override string Name => "Crust respawn delay";
    public override string Description => "Mega Crust and Alpha Crust respawn after 10 seconds, like the Alquamos, instead of 3.";
    public override UpdateVersion Version => UpdateVersion.CrustRespawnDelay;
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
    public override bool IsMandatory => true;
    public override DateTime CreatedAt => new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);

    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var monster in gameConfiguration.Monsters.Where(m => m.Number is MegaCrustNumber or AlphaCrustNumber))
        {
            monster.RespawnDelay = RespawnDelay;
        }

        return ValueTask.CompletedTask;
    }
}
