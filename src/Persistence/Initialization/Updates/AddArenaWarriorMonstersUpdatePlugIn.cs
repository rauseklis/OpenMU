// <copyright file="AddArenaWarriorMonstersUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the eleven Arena Warrior monsters (1100-1110) and their Arena spawns to existing databases. It only adds monster
/// definitions and Arena spawn areas that are missing, so running it again changes nothing.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("7C1E5A92-3B64-4D08-A1F7-52E9C0B8D346")]
public class AddArenaWarriorMonstersUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    internal const string PlugInName = "Add Arena Warrior Monsters";

    /// <summary>
    /// Gets the plugin description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Arena Warrior monsters 1100-1110 and their Arena map spawns.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddArenaWarriorMonsters;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        ArenaWarriorMonsters.AddMissing(context, gameConfiguration);
        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == Version075.Maps.Arena.Number) is { } arena)
        {
            ArenaWarriorMonsters.AddSpawns(context, arena, gameConfiguration);
        }

        return ValueTask.CompletedTask;
    }
}
