// <copyright file="AddTierTwoAncientStatsUpdatePlugIn.cs" company="MUnique">
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
[Guid("F8A19668-7D63-45DB-8050-26CB35F685C1")]
public sealed class AddTierTwoAncientStatsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Tier 2 Ancient full-set stats";

    /// <inheritdoc />
    public override string Description => "Complete Garuda, Heras, Argo, Eplete, Semeden and Chamer sets add 50 Strength, Agility, Vitality and Energy; Command is unchanged.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.TierTwoAncientStats;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new TierTwoAncientStats(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
