// <copyright file="ConfigureCherryWeightedExchangesUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Updates only the Noria Cherry Blossom exchanges.
/// </summary>
[PlugIn]
[Guid("C62DD0F2-A816-4608-A05F-B210401DD345")]
public sealed class ConfigureCherryWeightedExchangesUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Cherry Blossom weighted Red and Golden exchanges";

    /// <inheritdoc />
    public override string Description => "Adds Firecracker and Green Ribbon to Red, lowers pet chances, and splits Golden Silver and Luck boxes.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.CherryWeightedExchanges;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new CherryBlossomRewards(context, gameConfiguration).ConfigureExchanges();
        return ValueTask.CompletedTask;
    }
}
