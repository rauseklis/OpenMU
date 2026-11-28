// <copyright file="StartDarkInvasionNoriaChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command which lets a game master start the Dark Skeleton invasion in Noria right away, on the game server where it is typed.
/// </summary>
[PlugIn]
[Display(Name = "Start Dark Skeleton Invasion in Noria Chat Command", Description = "Handles the GM command /startdarknoria, which starts the Dark Skeleton invasion in Noria (plus Kalima) immediately on this game server.")]
[Guid("9C3E7B52-1A84-4F6D-B2C9-58E1D04A7F36")]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
public class StartDarkInvasionNoriaChatCommandPlugIn : StartDarkInvasionChatCommandPlugInBase
{
    private const string Command = "/startdarknoria";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    protected override ushort MainMapId => InvasionMaps.Noria;
}
