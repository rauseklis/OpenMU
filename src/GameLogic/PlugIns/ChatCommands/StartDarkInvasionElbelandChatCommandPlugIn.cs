// <copyright file="StartDarkInvasionElbelandChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command which lets a game master start the Dark Skeleton invasion in Elbeland right away, on the game server where it is typed.
/// </summary>
[PlugIn]
[Display(Name = "Start Dark Skeleton Invasion in Elbeland Chat Command", Description = "Handles the GM command /startdarkelbeland, which starts the Dark Skeleton invasion in Elbeland (plus Kalima) immediately on this game server.")]
[Guid("D41B6E83-7C25-49A0-9F3E-2B8C5A16E074")]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
public class StartDarkInvasionElbelandChatCommandPlugIn : StartDarkInvasionChatCommandPlugInBase
{
    private const string Command = "/startdarkelbeland";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    protected override ushort MainMapId => InvasionMaps.Elbeland;
}
