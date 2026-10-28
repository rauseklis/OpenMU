// <copyright file="ConfigureWorldGameplayParityUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>Enables matching gameplay bonus settings on worlds sharing a configuration.</summary>
[PlugIn]
[Guid("A3B67F28-4D52-4D17-924E-FF0640AE1258")]
public sealed class ConfigureWorldGameplayParityUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "World gameplay parity";

    /// <inheritdoc />
    public override string Description => "Enables the shared absolute monster bonus progression on every world, preserving experience rates and player data.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.WorldGameplayParity;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var server in (await context.GetAsync<GameServerDefinition>().ConfigureAwait(false))
            .Where(server => server.GameConfiguration is { } configuration && configuration.GetId() == gameConfiguration.GetId()))
        {
            server.SmallBonusDropsEnabled = true;
            server.SmallBonusDropChance = null;
        }
    }
}
