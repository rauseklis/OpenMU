// <copyright file="SwitchVaultAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Vault;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.GameLogic.Views.Vault;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Action which switches the opened vault of a player to another one of the account.
/// </summary>
public class SwitchVaultAction
{
    /// <summary>
    /// Switches the opened vault of a player to another one of the account.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="vaultNumber">The number of the requested vault, starting with 1.</param>
    public async ValueTask SwitchVaultAsync(Player player, byte vaultNumber)
    {
        if (player.Account is null
            || player.Vault is null
            || player.PlayerState.CurrentState != PlayerState.NpcDialogOpened
            || player.OpenedNpc?.Definition.NpcWindow != NpcWindow.VaultStorage)
        {
            return;
        }

        if (vaultNumber < 1 || vaultNumber > InventoryConstants.VaultCount)
        {
            await player.InvokeViewPlugInAsync<IUpdateVaultNumberPlugIn>(p => p.UpdateVaultNumberAsync(false)).ConfigureAwait(false);
            return;
        }

        if (vaultNumber != player.ActiveVaultNumber)
        {
            var storage = player.GetOrCreateVaultStorage(vaultNumber);
            var warehouseSize = player.Account.IsVaultExtended ? InventoryConstants.WarehouseSize * 2 : InventoryConstants.WarehouseSize;
            player.Vault = new Storage(warehouseSize, storage);
            player.ActiveVaultNumber = vaultNumber;
        }

        await player.InvokeViewPlugInAsync<IUpdateVaultNumberPlugIn>(p => p.UpdateVaultNumberAsync(true)).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IShowMerchantStoreItemListPlugIn>(p => p.ShowMerchantStoreItemListAsync(player.Vault.ItemStorage.Items, StoreKind.Normal)).ConfigureAwait(false);
    }
}
