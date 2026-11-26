// <copyright file="RequestMode.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

/// <summary>
/// How a player reacts to incoming requests of other players (party, trade, duel, guild, guild war).
/// It is set with the chat command /re.
/// </summary>
public enum RequestMode
{
    /// <summary>
    /// Requests are shown to the player, who can accept or refuse them (default).
    /// </summary>
    On,

    /// <summary>
    /// Requests are refused at once, the player does not see them. The requester is told that the player does not accept requests.
    /// </summary>
    Off,

    /// <summary>
    /// Party requests are accepted automatically; every other request is shown to the player.
    /// </summary>
    Auto,
}
