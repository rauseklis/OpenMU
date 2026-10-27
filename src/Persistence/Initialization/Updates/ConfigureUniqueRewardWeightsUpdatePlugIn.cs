// <copyright file="ConfigureUniqueRewardWeightsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Updates only global equipment reward weights.
/// </summary>
[PlugIn]
[Guid("83B84443-D654-49D2-92B7-FED4D129AA52")]
public sealed class ConfigureUniqueRewardWeightsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Unique global equipment reward weights";

    /// <inheritdoc />
    public override string Description => "Normalizes unique armor sets and removes repeated weapon and shield probability contributions.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.UniqueRewardWeights;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new UniqueRewardWeights(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
