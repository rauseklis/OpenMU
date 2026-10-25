// <copyright file="ReusableEventTicket.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.MiniGames;

/// <summary>
/// Identifies reusable entrance tickets independently of how they were obtained.
/// </summary>
internal static class ReusableEventTicket
{
    internal const byte Group = 13;
    internal const short DevilSquareNumber = 46;
    internal const short BloodCastleNumber = 47;
    internal const short KalimaNumber = 48;

    internal static bool IsValid(Item item, MiniGameDefinition game)
    {
        var number = game.Type switch
        {
            MiniGameType.DevilSquare => DevilSquareNumber,
            MiniGameType.BloodCastle => BloodCastleNumber,
            _ => (short)-1,
        };
        return item.Definition is { Group: Group } definition
            && definition.Number == number
            && item.Durability >= 1;
    }
}
