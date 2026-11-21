// <copyright file="UnlockVaultThrottleTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Vault;

/// <summary>
/// Tests that the vault PIN can not be guessed by trying again and again.
/// </summary>
[TestFixture]
public class UnlockVaultThrottleTests
{
    private DateTime _now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private UnlockVaultAction _action = null!;

    /// <summary>Creates an action with its own throttle and a clock which the test controls.</summary>
    [SetUp]
    public void SetUp() => this._action = new UnlockVaultAction(new VaultPinThrottle(() => this._now, 5, TimeSpan.FromMinutes(5)));

    /// <summary>The correct PIN unlocks the vault.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CorrectPinUnlocksAsync()
    {
        var player = await CreatePlayerAsync("acct-a").ConfigureAwait(false);

        await this._action.UnlockVaultAsync(player, "1234").ConfigureAwait(false);

        Assert.That(player.IsVaultLocked, Is.False);
    }

    /// <summary>After the allowed number of wrong tries even the correct PIN is refused until the lock ran out.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TooManyWrongPinsLockTheGuessingAsync()
    {
        var player = await CreatePlayerAsync("acct-b").ConfigureAwait(false);
        for (var i = 0; i < 5; i++)
        {
            await this._action.UnlockVaultAsync(player, "0000").ConfigureAwait(false);
        }

        await this._action.UnlockVaultAsync(player, "1234").ConfigureAwait(false);
        Assert.That(player.IsVaultLocked, Is.True, "the correct PIN is refused while the lock is active");

        this._now = this._now.AddMinutes(5).AddSeconds(1);
        await this._action.UnlockVaultAsync(player, "1234").ConfigureAwait(false);
        Assert.That(player.IsVaultLocked, Is.False, "the correct PIN works again after the lock ran out");
    }

    /// <summary>Logging in again does not reset the counter of the account.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ReconnectingDoesNotResetTheCounterAsync()
    {
        var first = await CreatePlayerAsync("acct-c").ConfigureAwait(false);
        for (var i = 0; i < 5; i++)
        {
            await this._action.UnlockVaultAsync(first, "0000").ConfigureAwait(false);
        }

        var second = await CreatePlayerAsync("acct-c").ConfigureAwait(false);
        await this._action.UnlockVaultAsync(second, "1234").ConfigureAwait(false);

        Assert.That(second.IsVaultLocked, Is.True);
    }

    /// <summary>A success clears the failures, and other accounts are not affected by somebody else's failures.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SuccessClearsFailuresAndAccountsAreIndependentAsync()
    {
        var player = await CreatePlayerAsync("acct-d").ConfigureAwait(false);
        var other = await CreatePlayerAsync("acct-e").ConfigureAwait(false);
        for (var i = 0; i < 4; i++)
        {
            await this._action.UnlockVaultAsync(player, "0000").ConfigureAwait(false);
        }

        await this._action.UnlockVaultAsync(player, "1234").ConfigureAwait(false);
        player.IsVaultLocked = true;
        for (var i = 0; i < 4; i++)
        {
            await this._action.UnlockVaultAsync(player, "0000").ConfigureAwait(false);
        }

        await this._action.UnlockVaultAsync(player, "1234").ConfigureAwait(false);
        Assert.That(player.IsVaultLocked, Is.False, "four wrong tries after a success are still below the limit");

        for (var i = 0; i < 5; i++)
        {
            await this._action.UnlockVaultAsync(other, "0000").ConfigureAwait(false);
        }

        player.IsVaultLocked = true;
        await this._action.UnlockVaultAsync(player, "1234").ConfigureAwait(false);
        Assert.That(player.IsVaultLocked, Is.False, "another account's failures do not lock this account");
    }

    private static async Task<Player> CreatePlayerAsync(string loginName)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Account!.LoginName = loginName;
        player.Account.VaultPassword = "1234";
        player.IsVaultLocked = true;
        return player;
    }
}
