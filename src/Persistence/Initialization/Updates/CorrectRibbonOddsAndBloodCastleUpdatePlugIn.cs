// <copyright file="CorrectRibbonOddsAndBloodCastleUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Configures the approved ribbon box contents.
/// </summary>
[PlugIn]
[Guid("A75A6CAB-B7E1-4989-85E8-4A007F13BD58")]
public sealed class CorrectRibbonOddsAndBloodCastleUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Ribbon opening odds and Blood Castle rewards";

    /// <inheritdoc />
    public override string Description => "Corrects ribbon item-or-Zen odds and Blood Castle level one through seven completion payouts.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.RibbonOpeningOddsAndBloodCastle;

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
        new BloodCastleCompletionRewards(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
