// <copyright file="ConfigureDevilSquareRewardsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Configures the reviewed Devil Square completion payouts.
/// </summary>
[PlugIn]
[Guid("6D11B1A6-6AF8-4EF0-B75D-9C4AF95F294E")]
public sealed class ConfigureDevilSquareRewardsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Devil Square completion rewards";

    /// <inheritdoc />
    public override string Description => "Sets reviewed Devil Square Zen and EXP payouts and first-place inventory items for levels one through six.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.DevilSquareCompletionRewards;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new DevilSquareZenRewards(context, gameConfiguration).Initialize();
        new DevilSquareExperienceRewards(context, gameConfiguration).Initialize();
        new DevilSquareFirstPlaceItems(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
