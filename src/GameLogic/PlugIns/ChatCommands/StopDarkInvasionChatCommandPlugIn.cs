// <copyright file="StopDarkInvasionChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command which lets a game master end the Dark Skeleton invasion right away, on the game server where it is typed.
/// </summary>
[PlugIn]
[Display(Name = "Stop Dark Skeleton Invasion Chat Command", Description = "Handles the GM command /stopdark, which ends the Dark Skeleton invasion immediately on this game server.")]
[Guid("5A2C9E47-D813-4B6F-A0E2-7C4B1F9D3E68")]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
public class StopDarkInvasionChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/stopdark";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

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

        if (!invasion.StopByGameMaster(player.GameContext))
        {
            await player.ShowBlueMessageAsync("The Dark Skeleton invasion is not running.").ConfigureAwait(false);
            return;
        }

        await player.ShowBlueMessageAsync("The Dark Skeleton invasion is ending now.").ConfigureAwait(false);
    }
}
