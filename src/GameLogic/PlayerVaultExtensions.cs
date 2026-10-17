// <copyright file="PlayerVaultExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Extensions to access the multiple vaults of a <see cref="Player"/>.
/// </summary>
public static class PlayerVaultExtensions
{
    /// <summary>
    /// Gets the item storage of the vault with the specified number. If it doesn't exist yet, it is created.
    /// Vault number 1 is <see cref="Account.Vault"/>, the others are stored in <see cref="Account.ExtraVaults"/>.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="vaultNumber">The number of the vault, starting with 1.</param>
    /// <returns>The item storage of the vault.</returns>
    public static ItemStorage GetOrCreateVaultStorage(this Player player, byte vaultNumber)
    {
        var account = player.Account ?? throw new InvalidOperationException("The player has no account.");
        if (vaultNumber <= 1)
        {
            return account.Vault ??= player.PersistenceContext.CreateNew<ItemStorage>();
        }

        var storage = account.ExtraVaults.FirstOrDefault(v => v.VaultNumber == vaultNumber);
        if (storage is null)
        {
            storage = player.PersistenceContext.CreateNew<ItemStorage>();
            storage.VaultNumber = vaultNumber;
            account.ExtraVaults.Add(storage);
        }

        return storage;
    }
}
