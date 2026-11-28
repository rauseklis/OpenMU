// <copyright file="StartDarkInvasionLorenciaChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command which lets a game master start the Dark Skeleton invasion in Lorencia right away, on the game server where it is typed.
/// </summary>
[PlugIn]
[Display(Name = "Start Dark Skeleton Invasion in Lorencia Chat Command", Description = "Handles the GM command /startdarklorencia, which starts the Dark Skeleton invasion in Lorencia (plus Kalima) immediately on this game server.")]
[Guid("2F6A8C14-3B57-4D92-8E1A-C5D0B7964F23")]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
public class StartDarkInvasionLorenciaChatCommandPlugIn : StartDarkInvasionChatCommandPlugInBase
{
    private const string Command = "/startdarklorencia";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    protected override ushort MainMapId => InvasionMaps.Lorencia;
}
