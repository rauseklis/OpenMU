// <copyright file="ChangeDarkReaperAttackDelayUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The Dark Reaper (1112) hits once every 5 seconds instead of every 2 seconds. Nothing else is changed.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("A4E7C2D9-3B18-4F6A-8D51-7E2B9C0F4A63")]
public class ChangeDarkReaperAttackDelayUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    internal const string PlugInName = "Change Dark Reaper Attack Delay";

    /// <summary>
    /// Gets the plugin description.
    /// </summary>
    internal const string PlugInDescription = "The Dark Reaper hits once every 5 seconds.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ChangeDarkReaperAttackDelay;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 27, 10, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        DarkSkeletonMonsters.SetAttackDelay(gameConfiguration, DarkSkeletonMonsters.DarkReaperNumber);
        return ValueTask.CompletedTask;
    }
}
