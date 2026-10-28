// <copyright file="ConfigureChaosCastleBoxRewardsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;
using MUnique.OpenMU.PlugIns;

/// <summary>Installs the approved Chaos Castle Ancient box payouts.</summary>
[PlugIn]
[Guid("B447FA17-F945-453E-9084-2FA94517C683")]
public sealed class ConfigureChaosCastleBoxRewardsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Chaos Castle Ancient box rewards";

    /// <inheritdoc />
    public override string Description => "Replaces CC Ancient equipment with approved tier boxes and chances, preserving the independent jewel reward.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ChaosCastleBoxRewards;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new ChaosCastleBoxRewards(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
