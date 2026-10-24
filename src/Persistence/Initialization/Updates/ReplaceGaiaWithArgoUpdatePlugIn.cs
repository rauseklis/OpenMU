// <copyright file="ReplaceGaiaWithArgoUpdatePlugIn.cs" company="MUnique">
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
[Guid("A6531C23-A172-49CF-B498-05775CDA4EF4")]
public sealed class ReplaceGaiaWithArgoUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Argo Spirit replaces Gaia in Tier 2";

    /// <inheritdoc />
    public override string Description => "Replace Gaia Silver Box rewards and its additional Tier 2 stats with Argo Spirit; original Gaia options remain.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ArgoSpiritTierTwo;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new ArgoSpiritTierTwo(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
