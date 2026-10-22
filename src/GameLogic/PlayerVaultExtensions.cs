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
    /// Gets the first vault of the game server the player is connected to, which also holds the vault Zen of this
    /// game server. It is <c>null</c> as long as the player never opened a vault on this game server.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The first vault of the player's game server.</returns>
    public static ItemStorage? GetMainVault(this Player player)
    {
        var account = player.Account;
        if (account is null)
        {
            return null;
        }

        var serverId = player.GetGameServerId();
        return serverId == 0 ? account.Vault : FindExtraVault(account, serverId, 1);
    }

    /// <summary>
    /// Gets the item storage of the vault with the specified number on the game server of the player. If it doesn't
    /// exist yet, it is created. Every game server has its own vaults: on the first server vault number 1 is
    /// <see cref="Account.Vault"/> and the others are stored in <see cref="Account.ExtraVaults"/>; on the other
    /// servers all of them are in <see cref="Account.ExtraVaults"/>.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="vaultNumber">The number of the vault, starting with 1.</param>
    /// <returns>The item storage of the vault.</returns>
    public static ItemStorage GetOrCreateVaultStorage(this Player player, byte vaultNumber)
    {
        var account = player.Account ?? throw new InvalidOperationException("The player has no account.");
        var serverId = player.GetGameServerId();
        vaultNumber = Math.Max(vaultNumber, (byte)1);
        if (serverId == 0 && vaultNumber == 1)
        {
            return account.Vault ??= player.PersistenceContext.CreateNew<ItemStorage>();
        }

        var storage = FindExtraVault(account, serverId, vaultNumber);
        if (storage is null)
        {
            storage = player.PersistenceContext.CreateNew<ItemStorage>();
            storage.VaultNumber = vaultNumber;
            storage.GameServerId = serverId;
            account.ExtraVaults.Add(storage);
        }

        return storage;
    }

    private static ItemStorage? FindExtraVault(Account account, byte serverId, byte vaultNumber)
        => account.ExtraVaults.FirstOrDefault(v => v.GameServerId == serverId && v.VaultNumber == vaultNumber);
}
