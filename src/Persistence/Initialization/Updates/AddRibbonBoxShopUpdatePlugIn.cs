// <copyright file="AddRibbonBoxShopUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds ribbon boxes to Lumen's shop without replacing existing stock.
/// </summary>
[PlugIn]
[Guid("B6A94F45-7420-4BD8-8975-0718D7EAD6C8")]
public sealed class AddRibbonBoxShopUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Ribbon boxes in Lumen's shop";

    /// <inheritdoc />
    public override string Description => "Adds red, green and blue ribbon boxes to the Lorencia bar for 1,000 Zen each.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.RibbonBoxShop;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new RibbonBoxShop(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
