// <copyright file="ChangeDarkSkeletonDropsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Replaces the drops of the dark skeletons by their new box drops (see <see cref="DarkSkeletonMonsters"/>): one box per
/// kill, Box of Heaven 45%, Firecracker (Dark Skeleton) or Star of the Sacred Birth (Dark Reaper) 45%, chocolate box 10%.
/// The old drop groups are deleted, so running it again gives the same result.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("5D19B7E2-C64A-4F0B-9E83-2A7F61D0C4B5")]
public class ChangeDarkSkeletonDropsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    internal const string PlugInName = "Change Dark Skeleton Drops";

    /// <summary>
    /// Gets the plugin description.
    /// </summary>
    internal const string PlugInDescription = "Dark Skeleton and Dark Reaper drop one box per kill: Box of Heaven 45%, Firecracker or Star 45%, Pink or Blue Chocolate Box 10%.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ChangeDarkSkeletonDrops;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 27, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        await DarkSkeletonMonsters.ReplaceDropsAsync(context, gameConfiguration).ConfigureAwait(false);
    }
}
