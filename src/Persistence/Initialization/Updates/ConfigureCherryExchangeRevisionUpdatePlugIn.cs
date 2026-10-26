// <copyright file="ConfigureCherryExchangeRevisionUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Updates only the Noria Cherry Blossom exchanges.
/// </summary>
[PlugIn]
[Guid("A370A520-130B-44D8-B0D6-906084C15B3A")]
public sealed class ConfigureCherryExchangeRevisionUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Cherry Blossom equal exchange rewards";

    /// <inheritdoc />
    public override string Description => "Replaces Red and Golden box rewards and gives six equal outcomes per leaf exchange.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.CherryExchangeRevision;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new CherryBlossomRewards(context, gameConfiguration).ConfigureExchanges();
        return ValueTask.CompletedTask;
    }
}
