// <copyright file="ConfigureBonusProgressionUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Installs the approved ordinary bonus progression once in existing Season 6 data.
/// </summary>
[PlugIn]
[Guid("A8429CB9-5A6F-4CF4-AE88-AEA5B9D3D201")]
public sealed class ConfigureBonusProgressionUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Ordinary bonus progression";

    /// <inheritdoc />
    public override string Description => "Replaces Star rewards, updates bonus monster assignments, books and Kundun category chances.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.BonusProgression;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new BonusProgression(context, gameConfiguration).Initialize();
        foreach (var server in (await context.GetAsync<GameServerDefinition>().ConfigureAwait(false))
            .Where(server => server.ServerID == 0 && server.GameConfiguration is { } configuration && configuration.GetId() == gameConfiguration.GetId()))
        {
            server.SmallBonusDropsEnabled = true;
            server.SmallBonusDropChance = 0.05;
        }
    }
}
