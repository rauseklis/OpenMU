// <copyright file="ConfigureKundunProgressionUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Installs the reviewed pre-Pink Kundun Excellent reward progression.
/// </summary>
[PlugIn]
[Guid("85E3D316-9DB7-41A7-9264-E39A3868B5B2")]
public sealed class ConfigureKundunProgressionUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Kundun pre-Pink Excellent progression";

    /// <inheritdoc />
    public override string Description => "Replaces only Kundun +1 through +5 pools and Excellent option probabilities.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.KundunProgression;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new KundunProgression(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
