// <copyright file="StartDarkInvasionDeviasChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command which lets a game master start the Dark Skeleton invasion in Devias right away, on the game server where it is typed.
/// </summary>
[PlugIn]
[Display(Name = "Start Dark Skeleton Invasion in Devias Chat Command", Description = "Handles the GM command /startdarkdevias, which starts the Dark Skeleton invasion in Devias (plus Kalima) immediately on this game server.")]
[Guid("B7D1E3A9-6C24-4F8E-91A5-3E0F7C2B8D46")]
[ChatCommandHelp(Command, CharacterStatus.GameMaster)]
public class StartDarkInvasionDeviasChatCommandPlugIn : StartDarkInvasionChatCommandPlugInBase
{
    private const string Command = "/startdarkdevias";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    protected override ushort MainMapId => InvasionMaps.Devias;
}
