// <copyright file="LoginAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions;

using System.Threading;
using MUnique.OpenMU.GameLogic.AntiCheat;
using MUnique.OpenMU.GameLogic.Views.Login;

/// <summary>
/// Action to log in a player to the game.
/// </summary>
public class LoginAction
{
    private const int AttemptsBeforeTakeOver = 3;

    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(5);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int Count, DateTime LastAttempt)> FailedAttempts = new();

    private static int _templateCounter;

    /// <summary>
    /// Logins the specified player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public async ValueTask LoginAsync(Player player, string username, string password)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());

        var state = await this.AuthenticateAsync(player, username, password).ConfigureAwait(false);
        if (state is null)
        {
            return;
        }

        if (!await this.ValidateAccountStateAsync(player, state.Value).ConfigureAwait(false))
        {
            return;
        }

        var (success, account) = await this.TryEstablishSessionAsync(player, username).ConfigureAwait(false);
        if (success && account is { })
        {
            try
            {
                await this.FinishLoginAsync(player, username, account).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Failed to finish login for [{Username}].", username);
                if (!account.IsTemplate && player.GameContext is IGameServerContext gameServerContext)
                {
                    await gameServerContext.LoginServer.LogOffAsync(username, gameServerContext.Id).ConfigureAwait(false);
                }

                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<AccountState?> AuthenticateAsync(Player player, string username, string password)
    {
        var config = AntiCheatState.GetConfiguration(player.GameContext);
        var address = (player as IHasIpAddress)?.IpAddress ?? "unknown";
        var accountHash = AntiCheatState.HashForLog(username);
        var throttleActive = config.LoginMode != AntiCheatMode.Off;
        var enforce = config.LoginMode == AntiCheatMode.Enforce;
        var slotAcquired = false;

        try
        {
            if (throttleActive)
            {
                var decision = AntiCheatState.Login.Check(username, address);
                if (decision != LoginThrottleDecision.Allow)
                {
                    AntiCheatState.LoginBlocked.Add(1);
                    player.Logger.LogInformation("[AC] rule=login.block mode={Mode} ip={Ip} acct={Acct} decision={Decision} action={Action}", config.LoginMode, address, accountHash, decision, enforce ? "block" : "allow");
                    if (enforce)
                    {
                        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
                        return null;
                    }
                }

                // In shadow mode we never wait; we only note that the check would have had to queue.
                var wait = enforce ? config : new AntiCheatConfiguration { LoginMaxConcurrentPasswordChecks = config.LoginMaxConcurrentPasswordChecks, LoginPasswordCheckQueueTimeoutMs = 1 };
                slotAcquired = await AntiCheatState.TryEnterPasswordCheckAsync(wait).ConfigureAwait(false);
                if (!slotAcquired)
                {
                    AntiCheatState.PasswordCheckTimeouts.Add(1);
                    player.Logger.LogInformation("[AC] rule=login.queue mode={Mode} ip={Ip} action={Action}", config.LoginMode, address, enforce ? "busy" : "allow");
                    if (enforce)
                    {
                        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
                        return null;
                    }
                }
            }

            var state = await player.PersistenceContext.AuthenticateAsync(username, password).ConfigureAwait(false);
            if (state is null)
            {
                player.Logger.LogInformation("Account not found or invalid password, username: [{Username}].", username);
                if (throttleActive)
                {
                    AntiCheatState.LoginFailures.Add(1);
                    var delay = AntiCheatState.Login.RecordFailure(username, address, config);
                    player.Logger.LogInformation("[AC] rule=login.fail mode={Mode} ip={Ip} acct={Acct} delayMs={Delay}", config.LoginMode, address, accountHash, enforce ? delay : 0);
                    if (enforce && delay > 0)
                    {
                        await Task.Delay(delay).ConfigureAwait(false);
                    }
                }

                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.InvalidPassword)).ConfigureAwait(false);
            }
            else if (throttleActive)
            {
                AntiCheatState.Login.RecordSuccess(username, address);
            }

            return state;
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Login Failed.");
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return null;
        }
        finally
        {
            if (slotAcquired)
            {
                AntiCheatState.ExitPasswordCheck();
            }
        }
    }

    private async ValueTask<bool> ValidateAccountStateAsync(Player player, AccountState state)
    {
        if (state == AccountState.Banned)
        {
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountBlocked)).ConfigureAwait(false);
            return false;
        }

        if (state == AccountState.TemporarilyBanned)
        {
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.TemporaryBlocked)).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async ValueTask<(bool Success, Account? Account)> TryEstablishSessionAsync(Player player, string username)
    {
        try
        {
            await using var context = await player.PlayerState.TryBeginAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
            if (!context.Allowed)
            {
                await this.HandleAlreadyConnectedAsync(player, username).ConfigureAwait(false);
                return (false, null);
            }

            if (player.GameContext is not IGameServerContext gameServerContext)
            {
                context.Allowed = false;
                return (false, null);
            }

            if (player.GameContext.OfflinePlayerManager.TryGetPlayer(username, out var offlinePlayer))
            {
                var isTemplateOffline = offlinePlayer!.IsTemplatePlayer;
                if (!isTemplateOffline)
                {
                    var (loggedIn, tookOver) = await this.TryLoginOrTakeOverAsync(player, gameServerContext, username).ConfigureAwait(false);
                    if (!loggedIn)
                    {
                        context.Allowed = false;
                        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected)).ConfigureAwait(false);
                        await gameServerContext.EventPublisher.PlayerAlreadyLoggedInAsync(gameServerContext.Id, username).ConfigureAwait(false);
                        return (false, null);
                    }

                    if (tookOver)
                    {
                        // The take-over already disconnected the offline session via DisconnectAccountAsync,
                        // so we skip HandleOfflineSessionHandoverAsync and just reload the account.
                        var reloadedAccount = await player.PersistenceContext.GetAccountByLoginNameAsync(username).ConfigureAwait(false);
                        if (reloadedAccount is null)
                        {
                            await gameServerContext.LoginServer.LogOffAsync(username, gameServerContext.Id).ConfigureAwait(false);
                            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
                            context.Allowed = false;
                            return (false, null);
                        }

                        return (true, reloadedAccount);
                    }
                }

                var offlineAccount = await this.HandleOfflineSessionHandoverAsync(player, username).ConfigureAwait(false);
                if (offlineAccount is null)
                {
                    if (!isTemplateOffline)
                    {
                        await gameServerContext.LoginServer.LogOffAsync(username, gameServerContext.Id).ConfigureAwait(false);
                    }

                    context.Allowed = false;
                    return (false, null);
                }

                return (true, offlineAccount);
            }

            var loadedAccount = await player.PersistenceContext.GetAccountByLoginNameAsync(username).ConfigureAwait(false);
            if (loadedAccount is null)
            {
                player.Logger.LogError("Failed to load account {Username} after authentication.", username);
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
                context.Allowed = false;
                return (false, null);
            }

            if (!loadedAccount.IsTemplate)
            {
                var (loggedIn, tookOver) = await this.TryLoginOrTakeOverAsync(player, gameServerContext, username).ConfigureAwait(false);
                if (!loggedIn)
                {
                    context.Allowed = false;
                    await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected)).ConfigureAwait(false);
                    await gameServerContext.EventPublisher.PlayerAlreadyLoggedInAsync(gameServerContext.Id, username).ConfigureAwait(false);
                    return (false, null);
                }

                if (tookOver)
                {
                    // The old session saved its progress while it was disconnected, so the data loaded above may be outdated.
                    loadedAccount = await player.PersistenceContext.GetAccountByLoginNameAsync(username).ConfigureAwait(false);
                    if (loadedAccount is null)
                    {
                        await gameServerContext.LoginServer.LogOffAsync(username, gameServerContext.Id).ConfigureAwait(false);
                        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
                        context.Allowed = false;
                        return (false, null);
                    }
                }
            }

            return (true, loadedAccount);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error during login through login server.");
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return (false, null);
        }
    }

    /// <summary>
    /// Tries to log in at the login server. If the account is stuck as online (for example after a client crash),
    /// the third failed attempt within a few minutes disconnects the old session, so the player gets in.
    /// </summary>
    private async ValueTask<(bool LoggedIn, bool TookOver)> TryLoginOrTakeOverAsync(Player player, IGameServerContext gameServerContext, string username)
    {
        var loginServer = gameServerContext.LoginServer;
        if (await loginServer.TryLoginAsync(username, gameServerContext.Id).ConfigureAwait(false))
        {
            FailedAttempts.TryRemove(username, out _);
            return (true, false);
        }

        if (!RegisterFailedAttempt(username))
        {
            return (false, false);
        }

        player.Logger.LogWarning("Account [{Username}] is still connected after {Attempts} login attempts. Disconnecting the old session.", username, AttemptsBeforeTakeOver);

        // Waits until the old session is disconnected and saved.
        await gameServerContext.EventPublisher.DisconnectAccountAsync(username).ConfigureAwait(false);

        // Frees the account even if no player of it exists anymore (stuck entry at the login server).
        await loginServer.LogOffAsync(username, gameServerContext.Id).ConfigureAwait(false);
        var success = await loginServer.TryLoginAsync(username, gameServerContext.Id).ConfigureAwait(false);
        return (success, success);
    }

    /// <summary>
    /// Counts a failed attempt. Returns true if this attempt is the one which takes over the account.
    /// </summary>
    /// <param name="username">The login name.</param>
    /// <returns>True, if this is the third failed attempt in a row.</returns>
    public static bool RegisterFailedAttempt(string username)
    {
        var now = DateTime.UtcNow;
        var entry = FailedAttempts.AddOrUpdate(
            username,
            _ => (1, now),
            (_, old) => now - old.LastAttempt > AttemptWindow ? (1, now) : (old.Count + 1, now));
        if (entry.Count < AttemptsBeforeTakeOver)
        {
            return false;
        }

        FailedAttempts.TryRemove(username, out _);
        return true;
    }

    private async ValueTask<Account?> HandleOfflineSessionHandoverAsync(Player player, string username)
    {
        player.Logger.LogInformation("Account {Username} has an active offline session. Stopping it and reloading account data.", username);

        try
        {
            await player.GameContext.OfflinePlayerManager.StopAsync(username).ConfigureAwait(false);
            var account = await player.PersistenceContext.GetAccountByLoginNameAsync(username).ConfigureAwait(false);
            if (account is null)
            {
                player.Logger.LogError("Failed to reload account {Username} after stopping offline session.", username);
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            }

            return account;
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Failed to reload account {Username} after stopping offline session.", username);
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return null;
        }
    }

    private async ValueTask HandleAlreadyConnectedAsync(Player player, string username)
    {
        var result = player.LoginResultOverride ?? LoginResult.AccountAlreadyConnected;
        player.LoginResultOverride = null;
        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(result)).ConfigureAwait(false);
        if (player.GameContext is IGameServerContext gameServerContext)
        {
            await gameServerContext.EventPublisher.PlayerAlreadyLoggedInAsync(gameServerContext.Id, username).ConfigureAwait(false);
        }
    }

    private async ValueTask FinishLoginAsync(Player player, string username, Account account)
    {
        await player.SetAccountAsync(account).ConfigureAwait(false);
        player.Logger.LogDebug("Login successful, username: [{Username}].", username);

        if (player.IsTemplatePlayer)
        {
            foreach (var character in account.Characters)
            {
                var counter = Interlocked.Increment(ref _templateCounter);
                character.Name = $"_{counter}";
            }
        }

        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.Ok)).ConfigureAwait(false);
        Combat.SessionEventPublisher.Publish(player, Combat.SessionEventKind.LoggedIn);
        if ((player as IConnectionTracked)?.TrackedConnection is { } trackedConnection)
        {
            AntiCheatState.Connections.MarkLoggedIn(trackedConnection);
        }
    }
}