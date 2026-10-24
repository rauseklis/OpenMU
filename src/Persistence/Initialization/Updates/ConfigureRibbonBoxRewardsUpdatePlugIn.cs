// <copyright file="ConfigureRibbonBoxRewardsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Configures the approved ribbon box contents.
/// </summary>
[PlugIn]
[Guid("DEB268BD-D2D2-494E-99F5-DC777F299F4F")]
public sealed class ConfigureRibbonBoxRewardsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Ribbon box contents";

    /// <inheritdoc />
    public override string Description => "Red: Archangel weapons; blue: Excellent HP jewelry; green: packs of ten jewels.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.RibbonBoxRewards;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new RibbonBoxRewards(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
