// -----------------------------------------------------------------------
// <copyright file="BlessJewelConsumeHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;

using System.Runtime.InteropServices;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.GameLogic.Pet;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Consume handler for upgrading items up to level 6 using the Jewel of Bless.
/// </summary>
[Guid("E95A0292-B3B4-4E8C-AC5A-7F3DB4F01A37")]
[PlugIn]
[Display(Name = nameof(PlugInResources.BlessJewelConsumeHandlerPlugIn_Name), Description = nameof(PlugInResources.BlessJewelConsumeHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
public class BlessJewelConsumeHandlerPlugIn : UpgradeItemLevelJewelConsumeHandlerPlugIn<BlessJewelConsumeHandlerPlugInConfiguration>
{
    /// <inheritdoc />
    public override ItemIdentifier Key => ItemConstants.JewelOfBless;

    /// <inheritdoc />
    public override object CreateDefaultConfig()
    {
        return new BlessJewelConsumeHandlerPlugInConfiguration
        {
            MaximumLevel = 5,
            MinimumLevel = 0,
            SuccessRatePercentage = 100,
            SuccessRateBonusWithLuckPercentage = 0,
            ResetToLevel0WhenFailMinLevel = 0,
        };
    }

    /// <inheritdoc />
    public override async ValueTask<bool> ConsumeItemAsync(Player player, Item item, Item? targetItem, FruitUsage fruitUsage)
    {
        if (targetItem?.Definition is not { Group: 13, Number: 37 })
        {
            return await base.ConsumeItemAsync(player, item, targetItem, fruitUsage).ConfigureAwait(false);
        }

        return await player.RunPersistenceExclusiveAsync(async () =>
        {
            var inventory = player.Inventory;
            if (!this.CheckPreconditions(player, item) || inventory is null || item.Durability < 1
                || item.ItemSlot <= InventoryConstants.LastEquippableItemSlotIndex
                || item.Definition is not { Group: 14, Number: 13 }
                || targetItem.ItemSlot <= InventoryConstants.LastEquippableItemSlotIndex
                || !ReferenceEquals(inventory.GetItem(item.ItemSlot), item)
                || !ReferenceEquals(inventory.GetItem(targetItem.ItemSlot), targetItem)
                || targetItem.IsRentalExpired() || targetItem.Durability >= targetItem.GetMaximumDurabilityOfOnePiece())
            {
                return false;
            }

            var creation = inventory.Items.FirstOrDefault(i => i.ItemSlot > InventoryConstants.LastEquippableItemSlotIndex
                && i.Definition is { Group: 14, Number: 22 } && i.Durability >= 1);
            if (creation is null)
            {
                return false;
            }

            // Validate everything before changing either jewel; serialized with normal player mutations.
            item.Durability -= 1;
            creation.Durability -= 1;
            targetItem.Durability = targetItem.GetMaximumDurabilityOfOnePiece();
            if (creation.Durability <= 0)
            {
                await player.DestroyInventoryItemAsync(creation).ConfigureAwait(false);
            }
            else
            {
                await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(creation, true)).ConfigureAwait(false);
            }

            await PetDurabilityPolicy.RefreshAsync(player, targetItem).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemUpgradedPlugIn>(p => p.ItemUpgradedAsync(targetItem)).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override bool ModifyItem(Item item, IContext persistenceContext)
    {
        // Fenrir repair must go through the two-jewel, player-aware path above.
        if (item.Definition is { Group: 13, Number: 37 })
        {
            return false;
        }

        if (this.Configuration?.RepairTargetItems.Contains(item.Definition!) is true
            && item.Durability < item.GetMaximumDurabilityOfOnePiece())
        {
            item.Durability = item.GetMaximumDurabilityOfOnePiece();
            return true;
        }

        return base.ModifyItem(item, persistenceContext);
    }
}