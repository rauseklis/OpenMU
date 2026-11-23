// <copyright file="BonusOpeningRedistributionUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sets the approved opening chances of Silver Medal, Gold Medal, Heart of Love, Firecracker and Box of Heaven.
/// </summary>
[PlugIn]
[Guid("5B8E0C4A-7D21-4F3E-9A66-2C1D84B7E5F3")]
public sealed class BonusOpeningRedistributionUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Bonus opening redistribution";

    /// <inheritdoc />
    public override string Description => "Jewels 1% each, new equipment shares for Silver, Gold, Heart of Love and Firecracker, new pet shares, and monster chances of 0.35% for Gold Medal, 0.30% for Heart of Love and 0.05% for Box of Heaven. Rings and Zen are kept.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.BonusOpeningRedistribution;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new BonusOpeningRedistribution(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
