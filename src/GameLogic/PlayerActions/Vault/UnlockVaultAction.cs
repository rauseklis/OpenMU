// <copyright file="UnlockVaultAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Vault;

using System.Security.Cryptography;
using System.Text;
using MUnique.OpenMU.GameLogic.AntiCheat;
using MUnique.OpenMU.GameLogic.Views.Vault;

/// <summary>
/// Action which unlocks the vault of a player, if the correct pin is provided.
/// </summary>
public class UnlockVaultAction
{
    private readonly VaultPinThrottle _throttle;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnlockVaultAction"/> class which uses the shared throttle.
    /// </summary>
    public UnlockVaultAction()
        : this(VaultPinThrottle.Shared)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnlockVaultAction"/> class.
    /// </summary>
    /// <param name="throttle">The throttle which limits the tries to guess a PIN.</param>
    public UnlockVaultAction(VaultPinThrottle throttle)
    {
        this._throttle = throttle;
    }

    /// <summary>
    /// Unlocks the vault of a player, if the correct pin is provided.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="pin">The pin.</param>
    public async ValueTask UnlockVaultAsync(Player player, string pin)
    {
        VaultLockChangeResult result;
        var account = player.Account?.LoginName;
        if (account is not null && !this._throttle.IsAllowed(account))
        {
            // Too many wrong tries: the PIN is not even looked at, so that the answer tells nothing.
            player.Logger.LogWarning("[AC] rule=vault.pin refused=locked account={Account}", AntiCheatState.HashForLog(account));
            result = VaultLockChangeResult.UnlockFailedByWrongPin;
        }
        else
        {
            var correct = player.Account?.VaultPassword is { } stored
                          && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(pin));
            if (account is not null)
            {
                this._throttle.Record(account, correct);
            }

            if (correct)
            {
                player.IsVaultLocked = false;
                result = VaultLockChangeResult.Unlocked;
            }
            else
            {
                result = VaultLockChangeResult.UnlockFailedByWrongPin;
            }
        }

        await player.InvokeViewPlugInAsync<IShowVaultLockChangeResponse>(p => p.ShowResponseAsync(result)).ConfigureAwait(false);
    }
}