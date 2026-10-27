// <copyright file="CashShopRequests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.CashShop;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.CashShop;

/// <summary>Authenticated, read-only Cash Shop requests shared by the network handler and integration tests.</summary>
internal static class CashShopRequests
{
    /// <summary>Builds the response using only server-authenticated account/server identity.</summary>
    /// <param name="player">The authenticated player.</param>
    /// <param name="packet">The complete request.</param>
    /// <param name="repository">Persistent balances, or null when unsupported.</param>
    /// <returns>A response or null when no reply is appropriate.</returns>
    internal static async ValueTask<byte[]?> HandleAsync(Player player, Memory<byte> packet, ICashShopRepository? repository)
    {
        if (!CashShopProtocol.IsValidRequest(packet.Span))
        {
            return null;
        }

        var subCode = packet.Span[3];
        var authenticated = player.Account is IIdentifiable && player.SelectedCharacter is not null
            && player.PlayerState.CurrentState == PlayerState.EnteredWorld;
        if (subCode == 0x02)
        {
            return packet.Span[4] == 1 ? null : CashShopProtocol.Open(authenticated && repository is not null);
        }

        if (!authenticated)
        {
            return null;
        }

        if (subCode == 0x01)
        {
            if (repository is null)
            {
                return null;
            }

            var account = (IIdentifiable)player.Account!;
            var balance = await repository.GetBalanceAsync(account.Id, player.GetGameServerId()).ConfigureAwait(false);
            return CashShopProtocol.Balance(balance);
        }

        return subCode == 0x05 ? CashShopProtocol.EmptyStorage() : CashShopProtocol.Refused(subCode);
    }
}
