// <copyright file="ConfigureRewardCategoryRatiosUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Updates only equipment category ratios.
/// </summary>
[PlugIn]
[Guid("0F1240CE-D267-4B4D-9563-606A6EAF7948")]
public sealed class ConfigureRewardCategoryRatiosUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Armor-focused equipment category ratios";

    /// <inheritdoc />
    public override string Description => "Scales equipment category ratios while preserving normalized internal weights, reward contents and non-equipment outcomes.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.RewardCategoryRatios;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new RewardCategoryRatios(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
