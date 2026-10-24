// <copyright file="AddTierOneAncientStatsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the approved stat bonuses without replacing existing Ancient options.
/// </summary>
[PlugIn]
[Guid("C261415A-5FB9-40BB-983D-03A3ACB00ED6")]
public sealed class AddTierOneAncientStatsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Tier 1 Ancient full-set stats";

    /// <inheritdoc />
    public override string Description => "Full Hyperion, Barnake and Fase sets add 100 Strength, Agility, Vitality and Energy; Cloud adds 75 each.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.TierOneAncientStats;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new TierOneAncientStats(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
