// <copyright file="KalimaTicketConsumeHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.PlayerActions.MiniGames;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Opens a level-appropriate Kalima portal and consumes one use only when creation succeeds.
/// </summary>
[PlugIn]
[Guid("86823C7C-1717-484C-A985-9423D9416043")]
public sealed class KalimaTicketConsumeHandlerPlugIn : BaseConsumeHandlerPlugIn
{
    /// <inheritdoc />
    public override ItemIdentifier Key => new(ReusableEventTicket.KalimaNumber, ReusableEventTicket.Group);

    /// <inheritdoc />
    public override async ValueTask<bool> ConsumeItemAsync(Player player, Item item, Item? targetItem, FruitUsage fruitUsage)
    {
        if (!this.CheckPreconditions(player, item)
            || item.Durability < 1
            || player.CurrentMap is not { } map
            || KalimaGateCreator.IsKalima(map.Definition.Number))
        {
            return false;
        }

        var level = KalimaGateCreator.GetLevel(player);
        if (!await KalimaGateCreator.CreateAsync(player, level, player.Position).ConfigureAwait(false))
        {
            return false;
        }

        await this.ConsumeSourceItemAsync(player, item).ConfigureAwait(false);
        return true;
    }
}
