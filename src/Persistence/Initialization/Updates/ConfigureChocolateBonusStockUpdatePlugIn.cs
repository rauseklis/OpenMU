// <copyright file="ConfigureChocolateBonusStockUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Installs missing bonus test stock and empty Red/Blue Chocolate rewards.
/// </summary>
[PlugIn]
[Guid("8C7425D7-F196-4EB8-81ED-EB817B6292ED")]
public sealed class ConfigureChocolateBonusStockUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Bonus test stock and empty Chocolate boxes";

    /// <inheritdoc />
    public override string Description => "Adds missing reviewed bonuses to Lumen and makes Red/Blue Chocolate openings empty.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ChocolateBonusStock;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new ChocolateBonusStock(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
