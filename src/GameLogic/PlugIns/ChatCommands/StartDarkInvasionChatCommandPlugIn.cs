// <copyright file="StartDarkInvasionChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command which lets a game master start the Dark Skeleton invasion right away.
/// </summary>
[PlugIn]
[Display(Name = "Start Dark Skeleton Invasion Chat Command", Description = "Handles the GM command /startdark, which starts the Dark Skeleton invasion immediately.")]
[Guid("B7D1E3A9-6C24-4F8E-91A5-3E0F7C2B8D46")]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
public class StartDarkInvasionChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/startdark";

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

        invasion.StartByGameMaster();
        await player.ShowBlueMessageAsync("The Dark Skeleton invasion is starting now.").ConfigureAwait(false);
    }
}
