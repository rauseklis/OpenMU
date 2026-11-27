// <copyright file="AddDarkSkeletonMonstersUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the dark skeletons (Dark Skeleton 1111, Dark Reaper 1112) and their Devias test spawns to existing databases.
/// It only adds monster definitions and spawn areas that are missing, so running it again changes nothing.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("3E8B4D17-9A2C-4F65-B0D3-71C6E25A9F48")]
public class AddDarkSkeletonMonstersUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    internal const string PlugInName = "Add Dark Skeleton Monsters";

    /// <summary>
    /// Gets the plugin description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Dark Skeleton (1111) and Dark Reaper (1112) monsters and one of each in Devias (221/81 and 222/85).";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddDarkSkeletonMonsters;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        DarkSkeletonMonsters.AddMissing(context, gameConfiguration);
        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == Version075.Maps.Devias.Number) is { } devias)
        {
            DarkSkeletonMonsters.AddDeviasSpawns(context, devias, gameConfiguration);
        }

        return ValueTask.CompletedTask;
    }
}
