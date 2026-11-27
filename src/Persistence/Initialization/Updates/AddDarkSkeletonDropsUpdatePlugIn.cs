// <copyright file="AddDarkSkeletonDropsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Gives the dark skeletons their box drops (see <see cref="DarkSkeletonMonsters"/>): one box per kill, and nothing else. Monsters which already have drops of their own are left alone,
/// so running it again changes nothing.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("A6C2F0D4-1B7E-4E39-8D52-6F04B9C3E781")]
public class AddDarkSkeletonDropsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    internal const string PlugInName = "Add Dark Skeleton Drops";

    /// <summary>
    /// Gets the plugin description.
    /// </summary>
    internal const string PlugInDescription = "Dark Skeleton and Dark Reaper drop one box per kill: Box of Heaven, Firecracker or Star, Pink or Blue Chocolate Box.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddDarkSkeletonDrops;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        DarkSkeletonMonsters.AddMissingDrops(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
