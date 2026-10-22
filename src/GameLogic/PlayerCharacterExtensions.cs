// <copyright file="PlayerCharacterExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Extensions which tell which characters of an account belong to the game server a player is connected to.
/// Every game server has its own characters (for example a 50x and a 5x experience server).
/// </summary>
public static class PlayerCharacterExtensions
{
    /// <summary>
    /// Gets the id of the game server the player is connected to.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The server id of the game server.</returns>
    public static byte GetGameServerId(this Player player)
        => player.GameContext is IGameServerContext serverContext ? serverContext.Id : (byte)0;

    /// <summary>
    /// Gets the characters of the player's account which live on the game server of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The characters of this game server.</returns>
    public static IEnumerable<Character> GetCharactersOfThisServer(this Player player)
    {
        var serverId = player.GetGameServerId();
        return player.Account?.Characters.Where(character => character.GameServerId == serverId) ?? Enumerable.Empty<Character>();
    }
}
