// <copyright file="PlayerCharacterExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Security.Cryptography;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;

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

    /// <summary>
    /// Gets the identifier under which the account keeps its data on the game server of the player (Jewel Bank, Auction
    /// House). On the first game server it is the account id itself, so the data which exists already stays as it is; every
    /// other game server gets an identifier of its own, so nothing is shared between the game servers.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The identifier, or <c>null</c> when the player has no account.</returns>
    public static Guid? GetServerScopedAccountId(this Player player)
        => player.Account is MUnique.OpenMU.Persistence.IIdentifiable account ? ScopeAccountId(account.Id, player.GetGameServerId()) : null;

    /// <summary>
    /// Derives the identifier under which an account keeps its data on a game server.
    /// </summary>
    /// <param name="accountId">The account identifier.</param>
    /// <param name="gameServerId">The game server id.</param>
    /// <returns>The account id for the first game server, otherwise a name-based identifier of account and server.</returns>
    public static Guid ScopeAccountId(Guid accountId, byte gameServerId)
    {
        if (gameServerId == 0)
        {
            return accountId;
        }

        Span<byte> input = stackalloc byte[17];
        accountId.TryWriteBytes(input);
        input[16] = gameServerId;
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        var bytes = hash[..16].ToArray();
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
