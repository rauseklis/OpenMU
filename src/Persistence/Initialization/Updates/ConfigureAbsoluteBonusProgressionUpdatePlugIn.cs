// <copyright file="ConfigureAbsoluteBonusProgressionUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Installs absolute monster bonus chances and fruit-free Heaven rewards.
/// </summary>
[PlugIn]
[Guid("F7A39DB3-B3F2-4206-975A-FB87A857D16F")]
public sealed class ConfigureAbsoluteBonusProgressionUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Absolute ordinary monster bonus progression";

    /// <inheritdoc />
    public override string Description => "Updates six absolute monster chances, level-derived Silver sources and Heaven categories.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AbsoluteBonusProgression;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new AbsoluteBonusProgression(context, gameConfiguration).Initialize();
        foreach (var server in (await context.GetAsync<GameServerDefinition>().ConfigureAwait(false))
            .Where(server => server.ServerID == 0 && server.GameConfiguration is { } configuration && configuration.GetId() == gameConfiguration.GetId()))
        {
            server.SmallBonusDropsEnabled = true;
            server.SmallBonusDropChance = null;
        }
    }
}
