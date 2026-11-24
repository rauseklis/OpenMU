// <copyright file="HealthLossPerHitUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The Imp (Satan), the Demon and the wings of the 1st, 2nd and 3rd class take health from the wearer with every hit.
/// </summary>
[PlugIn]
[Guid("3C6E1A47-8B52-4D09-A1F3-72E5D90B4C18")]
public sealed class HealthLossPerHitUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Health loss per hit for pets and wings";

    /// <inheritdoc />
    public override string Description => "Imp (Satan) 3, Demon 4, 1st class wings 1, 2nd class wings 3 and 3rd class wings 5 health points are lost with every hit.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.HealthLossPerHit;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new VersionSeasonSix.HealthLossPerHit(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
