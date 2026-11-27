// <copyright file="AddDarkMageUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the Dark Mage (1113): a dark skeleton with a Skull Staff whose attacks are shown as Flame, 10 000 health,
/// everything else 100, with its box drops, and one in Devias (223/83, respawn 10 s). Only adds what is missing.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("C8F3A61E-2D47-4B95-A0E6-93B5D17F2C04")]
public class AddDarkMageUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    internal const string PlugInName = "Add Dark Mage";

    /// <summary>
    /// Gets the plugin description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Dark Mage (1113, Skull Staff, Flame) with its box drops and one in Devias at 223/83.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddDarkMage;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 27, 20, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        DarkSkeletonMonsters.AddMissing(context, gameConfiguration);
        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == Version075.Maps.Devias.Number) is { } devias)
        {
            DarkSkeletonMonsters.AddDeviasSpawns(context, devias, gameConfiguration);
        }

        DarkSkeletonMonsters.AddMissingDrops(context, gameConfiguration);

        return ValueTask.CompletedTask;
    }
}
