// <copyright file="ItemCustodyPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Turns trades, sales, purchases and monster drops of items into <see cref="CustodyEvent"/>s. It changes nothing in the game.
/// </summary>
[PlugIn]
[Display(Name = "Item custody events", Description = "Reports items which were traded, sold, bought or issued by monsters to the item custody event consumers (for example the journal).")]
[Guid("6E2B8D47-1C93-4A50-B7F2-9D05A3C86E14")]
public sealed class ItemCustodyPlugIn : IItemTradedToOtherPlayerPlugIn, IItemSoldToOtherPlayerPlugIn, IItemBoughtFromMerchantPlugIn, IItemSoldToMerchantPlugIn, IMonsterItemDropPlugIn
{
    /// <inheritdoc />
    public void ItemTraded(ITrader source, ITrader target, Item item)
        => Publish(source.GameContext, CustodyEventKind.ItemTraded, item, (source as Player)?.SelectedCharacter, source.Name, (target as Player)?.SelectedCharacter, target.Name);

    /// <inheritdoc />
    void IItemSoldToOtherPlayerPlugIn.ItemSold(Player seller, Item item, Player buyer)
        => Publish(seller.GameContext, CustodyEventKind.ItemSoldToPlayer, item, seller.SelectedCharacter, seller.Name, buyer.SelectedCharacter, buyer.Name);

    /// <inheritdoc />
    public void ItemBought(Player player, Item item, Item sourceItem, NonPlayerCharacter merchant)
        => Publish(player.GameContext, CustodyEventKind.ItemBoughtFromMerchant, item, null, merchant.Definition.Designation, player.SelectedCharacter, player.Name);

    /// <inheritdoc />
    void IItemSoldToMerchantPlugIn.ItemSold(Player player, Item item, NonPlayerCharacter merchant)
        => Publish(player.GameContext, CustodyEventKind.ItemSoldToMerchant, item, player.SelectedCharacter, player.Name, null, merchant.Definition.Designation);

    /// <inheritdoc />
    public void ItemDropping(AttackableNpcBase monster, Player killer, Item item, CancelEventArgs eventArgs)
        => Publish(killer.GameContext, CustodyEventKind.ItemIssuedByMonster, item, null, monster.Definition.Designation, killer.SelectedCharacter, killer.Name);

    private static void Publish(IGameContext gameContext, CustodyEventKind kind, Item item, Character? from, string fromName, Character? to, string toName)
    {
        CustodyEventPublisher.Publish(
            gameContext.PlugInManager,
            new CustodyEvent(
                kind,
                DateTime.UtcNow,
                (item as IIdentifiable)?.Id,
                item.Definition?.Name.ValueInNeutralLanguage ?? string.Empty,
                item.Level,
                (from as IIdentifiable)?.Id,
                fromName,
                (to as IIdentifiable)?.Id,
                toName));
    }
}
