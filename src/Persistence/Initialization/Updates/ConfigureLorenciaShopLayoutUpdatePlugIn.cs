// <copyright file="ConfigureLorenciaShopLayoutUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Installs the horizontal Lorencia bar shop layout.
/// </summary>
[PlugIn]
[Guid("FCB6D459-11C9-4604-983D-669DC5F3C17B")]
public sealed class ConfigureLorenciaShopLayoutUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Lorencia bar horizontal shop layout";

    /// <inheritdoc />
    public override string Description => "Arranges progression, Kundun, Ribbon and Ancient rows with two empty horizontal rows below.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.LorenciaShopLayout;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new LorenciaShopLayout(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
