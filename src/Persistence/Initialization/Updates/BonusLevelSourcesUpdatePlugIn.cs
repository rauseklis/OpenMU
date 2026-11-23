// <copyright file="BonusLevelSourcesUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Makes the small bonuses drop by monster level on every map, including Kalima, Blood Castle and Devil Square.
/// </summary>
[PlugIn]
[Guid("C3D7A1E9-4B62-4F08-8E15-6A9B2D0F7C41")]
public sealed class BonusLevelSourcesUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Bonus sources by monster level";

    /// <inheritdoc />
    public override string Description => "Silver 1-44, Gold 45-59, Heart of Love 50-60, Firecracker 60-74, Box of Heaven 20-74 and Star 72-99, on every map including Kalima, Blood Castle and Devil Square. Chances per kill stay the same.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.BonusLevelSources;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 23, 13, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new BonusLevelSources(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
