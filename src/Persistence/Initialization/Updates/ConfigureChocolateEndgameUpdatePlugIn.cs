// <copyright file="ConfigureChocolateEndgameUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Installs reviewed ordinary Chocolate rewards and safely disables sockets on existing data.
/// </summary>
[PlugIn]
[Guid("30DBFDCE-B07D-410D-B49D-D93D684A3147")]
public sealed class ConfigureChocolateEndgameUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Chocolate endgame rewards and disabled sockets";

    /// <inheritdoc />
    public override string Description => "Configures Pink/Blue/Red ordinary rewards, mace/Summoner balance and legacy-safe socket disabling.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ChocolateEndgame;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new ChocolateEndgameRewards(context, gameConfiguration).Initialize();
        new ChocolateEndgameItemBalance(context, gameConfiguration).Initialize();
        new DisabledSocketSystem(context, gameConfiguration).Initialize();
        await DisabledSocketItemCleanup.ApplyAsync(context, gameConfiguration).ConfigureAwait(false);
    }
}
