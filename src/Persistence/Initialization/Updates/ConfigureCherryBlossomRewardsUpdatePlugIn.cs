// <copyright file="ConfigureCherryBlossomRewardsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Installs the approved Play-Box and branch exchanges in existing Season 6 data.
/// </summary>
[PlugIn]
[Guid("C47C09CC-37CD-48A5-9804-D9BB7C9FC4EA")]
public sealed class ConfigureCherryBlossomRewardsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Cherry Blossom rewards";

    /// <inheritdoc />
    public override string Description => "Adds White and Red branches and configures the 10/30/255 branch exchanges in Noria.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.CherryBlossomRewards;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new ReusableEventTicketItems(context, gameConfiguration).Initialize();
        new CherryBlossomRewards(context, gameConfiguration).Initialize();
        new CherryBlossomShop(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
