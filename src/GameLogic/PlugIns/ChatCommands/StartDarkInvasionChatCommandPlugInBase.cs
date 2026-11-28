// <copyright file="StartDarkInvasionChatCommandPlugInBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The base of the game master commands which start the Dark Skeleton invasion right away
/// with a certain main map, on the game server where the command is typed.
/// </summary>
public abstract class StartDarkInvasionChatCommandPlugInBase : IChatCommandPlugIn
{
    /// <inheritdoc />
    public abstract string Key { get; }

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <summary>
    /// Gets the main map of the invasion which this command starts.
    /// </summary>
    protected abstract ushort MainMapId { get; }

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var invasion = player.GameContext.PlugInManager
            .GetActivePlugInsOf<IPeriodicTaskPlugIn>()
            .OfType<DarkSkeletonInvasionPlugIn>()
            .FirstOrDefault();
        if (invasion is null)
        {
            await player.ShowBlueMessageAsync("The Dark Skeleton invasion is not active.").ConfigureAwait(false);
            return;
        }

        if (!invasion.IsEnabledOn(player.GameContext))
        {
            await player.ShowBlueMessageAsync("The Dark Skeleton invasion is not enabled on this server.").ConfigureAwait(false);
            return;
        }

        if (invasion.IsRunning(player.GameContext))
        {
            await player.ShowBlueMessageAsync("The Dark Skeleton invasion is already running.").ConfigureAwait(false);
            return;
        }

        if (await player.GameContext.GetMapAsync(this.MainMapId).ConfigureAwait(false) is null)
        {
            await player.ShowBlueMessageAsync("This map is not available on this server.").ConfigureAwait(false);
            return;
        }

        invasion.StartByGameMaster(player.GameContext, this.MainMapId);
        await player.ShowBlueMessageAsync("The Dark Skeleton invasion is starting now.").ConfigureAwait(false);
    }
}
