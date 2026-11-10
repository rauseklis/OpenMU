// <copyright file="WebsiteAccountService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Accounts;

using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using MUnique.OpenMU.WebsiteApi.Data;

/// <summary>The outcome of a registration.</summary>
public enum RegistrationOutcome
{
    /// <summary>The account was created.</summary>
    Created,

    /// <summary>The login name breaks <see cref="AccountRules"/>.</summary>
    InvalidName,

    /// <summary>The password breaks <see cref="AccountRules"/>.</summary>
    InvalidPassword,

    /// <summary>The e-mail address breaks <see cref="AccountRules"/>.</summary>
    InvalidEmail,

    /// <summary>The login name is taken (ignoring the case).</summary>
    NameTaken,

    /// <summary>The e-mail address is taken.</summary>
    EmailTaken,
}

/// <summary>The outcome of a password change.</summary>
public enum PasswordChangeOutcome
{
    /// <summary>The password was changed.</summary>
    Changed,

    /// <summary>The login name or the current password is wrong, or the account may not be used.</summary>
    InvalidCredentials,

    /// <summary>The new password breaks <see cref="AccountRules"/>.</summary>
    InvalidNewPassword,
}

/// <summary>The outcome of an e-mail change.</summary>
public enum EmailChangeOutcome
{
    /// <summary>The address was changed (or was the same already).</summary>
    Changed,

    /// <summary>The login name or the password is wrong, or the account may not be used.</summary>
    InvalidCredentials,

    /// <summary>The new address breaks <see cref="AccountRules"/>.</summary>
    InvalidEmail,

    /// <summary>The new address belongs to another account.</summary>
    EmailTaken,
}

/// <summary>The outcome of asking for an e-mail verification token.</summary>
public enum VerificationRequestOutcome
{
    /// <summary>A token was created.</summary>
    Issued,

    /// <summary>No such usable account.</summary>
    NotFound,

    /// <summary>The address is verified already.</summary>
    AlreadyVerified,

    /// <summary>The account has no e-mail address.</summary>
    NoEmail,
}

/// <summary>The outcome of using a reset token.</summary>
public enum PasswordResetOutcome
{
    /// <summary>The password was changed.</summary>
    Changed,

    /// <summary>The token is unknown, used, expired or malformed.</summary>
    InvalidToken,

    /// <summary>The new password breaks <see cref="AccountRules"/>; the token is still valid.</summary>
    InvalidPassword,
}

/// <summary>
/// The account logic of the website API: registration, credential check and password change. Hashing is
/// the same BCrypt call the game uses for its accounts (<c>BCrypt.Net.BCrypt.HashPassword</c>, verified with
/// <c>BCrypt.Verify</c>, like <c>AccountRepository.AuthenticateAsync</c>), so an account made here can log in to the game.
/// </summary>
public sealed class WebsiteAccountService
{
    private static readonly string DummyHash = BCrypt.HashPassword("not-a-real-password-for-timing-only");

    /// <summary>How long an e-mail verification token works.</summary>
    public static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);

    /// <summary>How long a password reset token works.</summary>
    public static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly IWebsiteStore _store;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SemaphoreSlim _registrationLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="WebsiteAccountService"/> class.
    /// </summary>
    /// <param name="store">The data store.</param>
    /// <param name="clock">The clock; defaults to the system clock.</param>
    public WebsiteAccountService(IWebsiteStore store, Func<DateTimeOffset>? clock = null)
    {
        this._store = store;
        this._clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Registers a new normal account.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="password">The password.</param>
    /// <param name="email">The e-mail address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome and, when created, the account id.</returns>
    public async Task<(RegistrationOutcome Outcome, Guid Id)> RegisterAsync(string? loginName, string? password, string? email, CancellationToken cancellationToken)
    {
        if (!AccountRules.IsValidLoginName(loginName))
        {
            return (RegistrationOutcome.InvalidName, Guid.Empty);
        }

        if (!AccountRules.IsValidPassword(password))
        {
            return (RegistrationOutcome.InvalidPassword, Guid.Empty);
        }

        if (!AccountRules.IsValidEmail(email))
        {
            return (RegistrationOutcome.InvalidEmail, Guid.Empty);
        }

        // One registration at a time: the e-mail address has no unique index in the database.
        await this._registrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await this._store.LoginNameExistsAsync(loginName!, cancellationToken).ConfigureAwait(false))
            {
                return (RegistrationOutcome.NameTaken, Guid.Empty);
            }

            if (await this._store.EmailExistsAsync(email!, cancellationToken).ConfigureAwait(false))
            {
                return (RegistrationOutcome.EmailTaken, Guid.Empty);
            }

            var hash = BCrypt.HashPassword(password!);
            var (outcome, id) = await this._store.CreateAccountAsync(loginName!, hash, email!, cancellationToken).ConfigureAwait(false);
            return outcome switch
            {
                CreateAccountOutcome.Created => (RegistrationOutcome.Created, id),
                CreateAccountOutcome.EmailTaken => (RegistrationOutcome.EmailTaken, Guid.Empty),
                _ => (RegistrationOutcome.NameTaken, Guid.Empty),
            };
        }
        finally
        {
            this._registrationLock.Release();
        }
    }

    /// <summary>
    /// Checks a login name and password.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account when the credentials are right and the account may be used on the website (not banned, template or bot); otherwise <c>null</c>.</returns>
    public async Task<AccountRecord?> VerifyAsync(string? loginName, string? password, CancellationToken cancellationToken)
    {
        // The same amount of work for unknown names, so the time does not tell whether the name exists.
        var account = string.IsNullOrEmpty(loginName) ? null : await this._store.FindAccountAsync(loginName, cancellationToken).ConfigureAwait(false);
        var passwordOk = false;
        try
        {
            passwordOk = BCrypt.Verify(password ?? string.Empty, account?.PasswordHash ?? DummyHash);
        }
        catch (SaltParseException)
        {
            // A stored value which is not a BCrypt hash never matches.
        }

        return account is not null && passwordOk && account.CanLogIn ? account : null;
    }

    /// <summary>
    /// Changes the password after checking the current one.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="currentPassword">The current password.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<PasswordChangeOutcome> ChangePasswordAsync(string? loginName, string? currentPassword, string? newPassword, CancellationToken cancellationToken)
    {
        var account = await this.VerifyAsync(loginName, currentPassword, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return PasswordChangeOutcome.InvalidCredentials;
        }

        if (!AccountRules.IsValidPassword(newPassword))
        {
            return PasswordChangeOutcome.InvalidNewPassword;
        }

        await this._store.UpdatePasswordHashAsync(account.Id, BCrypt.HashPassword(newPassword!), cancellationToken).ConfigureAwait(false);
        return PasswordChangeOutcome.Changed;
    }

    /// <summary>
    /// Gets the account page data. Banned, template and bot accounts and unknown names give <c>null</c>.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account and its e-mail details, or <c>null</c>.</returns>
    public async Task<(AccountRecord Account, AccountDetails Details)?> GetAccountAsync(string? loginName, CancellationToken cancellationToken)
    {
        if (!AccountRules.IsValidLoginName(loginName))
        {
            return null;
        }

        var account = await this._store.FindAccountAsync(loginName!, cancellationToken).ConfigureAwait(false);
        if (account is null || !account.CanLogIn)
        {
            return null;
        }

        var details = await this._store.GetAccountDetailsAsync(account.Id, cancellationToken).ConfigureAwait(false);
        return details is null ? null : (account, details);
    }

    /// <summary>
    /// Changes the e-mail address after checking the password. The new address is not verified and all open tokens are void.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="password">The password.</param>
    /// <param name="newEmail">The new address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<EmailChangeOutcome> ChangeEmailAsync(string? loginName, string? password, string? newEmail, CancellationToken cancellationToken)
    {
        var account = await this.VerifyAsync(loginName, password, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return EmailChangeOutcome.InvalidCredentials;
        }

        if (!AccountRules.IsValidEmail(newEmail))
        {
            return EmailChangeOutcome.InvalidEmail;
        }

        // Shares the lock with the registration: the e-mail address has no unique index in the database.
        await this._registrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var details = await this._store.GetAccountDetailsAsync(account.Id, cancellationToken).ConfigureAwait(false);
            if (details is not null && string.Equals(details.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                return EmailChangeOutcome.Changed;
            }

            if (await this._store.EmailExistsAsync(newEmail!, cancellationToken).ConfigureAwait(false))
            {
                return EmailChangeOutcome.EmailTaken;
            }

            await this._store.UpdateEmailAsync(account.Id, newEmail!, cancellationToken).ConfigureAwait(false);
            return EmailChangeOutcome.Changed;
        }
        finally
        {
            this._registrationLock.Release();
        }
    }

    /// <summary>
    /// Creates an e-mail verification token (the website sends the mail). A new token replaces an older one.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome and, when issued, the address and the token.</returns>
    public async Task<(VerificationRequestOutcome Outcome, string Email, string Token)> RequestEmailVerificationAsync(string? loginName, CancellationToken cancellationToken)
    {
        var found = await this.GetAccountAsync(loginName, cancellationToken).ConfigureAwait(false);
        if (found is not { } entry)
        {
            return (VerificationRequestOutcome.NotFound, string.Empty, string.Empty);
        }

        if (entry.Details.EmailVerified)
        {
            return (VerificationRequestOutcome.AlreadyVerified, string.Empty, string.Empty);
        }

        if (string.IsNullOrEmpty(entry.Details.Email))
        {
            return (VerificationRequestOutcome.NoEmail, string.Empty, string.Empty);
        }

        var token = await this.IssueTokenAsync(entry.Account.Id, TokenPurpose.VerifyEmail, VerificationTokenLifetime, cancellationToken).ConfigureAwait(false);
        return (VerificationRequestOutcome.Issued, entry.Details.Email, token);
    }

    /// <summary>
    /// Uses an e-mail verification token and marks the address as verified.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The login name, or <c>null</c> if the token is unknown, used, expired or malformed.</returns>
    public async Task<string?> ConfirmEmailVerificationAsync(string? token, CancellationToken cancellationToken)
    {
        if (!TryHashToken(token, out var hash))
        {
            return null;
        }

        var accountId = await this._store.ConsumeTokenAsync(TokenPurpose.VerifyEmail, hash, this._clock(), cancellationToken).ConfigureAwait(false);
        if (accountId is not { } id)
        {
            return null;
        }

        await this._store.SetEmailVerifiedAsync(id, cancellationToken).ConfigureAwait(false);
        return (await this._store.FindAccountByIdAsync(id, cancellationToken).ConfigureAwait(false))?.LoginName;
    }

    /// <summary>
    /// Creates a password reset token for the account with this e-mail address (the website sends the mail).
    /// Only normal accounts qualify (no GM, spectator, banned, template or bot accounts), and only if exactly one account has the address.
    /// </summary>
    /// <param name="email">The e-mail address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The login name, address and token, or <c>null</c> if there is none.</returns>
    public async Task<(string LoginName, string Email, string Token)?> RequestPasswordResetAsync(string? email, CancellationToken cancellationToken)
    {
        if (!AccountRules.IsValidEmail(email))
        {
            return null;
        }

        var accounts = await this._store.FindAccountsByEmailAsync(email!, cancellationToken).ConfigureAwait(false);
        if (accounts.Count != 1 || !CanReset(accounts[0]))
        {
            return null;
        }

        var details = await this._store.GetAccountDetailsAsync(accounts[0].Id, cancellationToken).ConfigureAwait(false);
        var token = await this.IssueTokenAsync(accounts[0].Id, TokenPurpose.ResetPassword, ResetTokenLifetime, cancellationToken).ConfigureAwait(false);
        return (accounts[0].LoginName, details?.Email ?? email!, token);
    }

    /// <summary>
    /// Sets a new password with a reset token. A wrong new password does not use the token up.
    /// All other tokens of the account are void afterwards.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<PasswordResetOutcome> ConfirmPasswordResetAsync(string? token, string? newPassword, CancellationToken cancellationToken)
    {
        if (!TryHashToken(token, out var hash)
            || await this._store.FindTokenAccountAsync(TokenPurpose.ResetPassword, hash, this._clock(), cancellationToken).ConfigureAwait(false) is null)
        {
            return PasswordResetOutcome.InvalidToken;
        }

        if (!AccountRules.IsValidPassword(newPassword))
        {
            return PasswordResetOutcome.InvalidPassword;
        }

        var accountId = await this._store.ConsumeTokenAsync(TokenPurpose.ResetPassword, hash, this._clock(), cancellationToken).ConfigureAwait(false);
        if (accountId is not { } id)
        {
            return PasswordResetOutcome.InvalidToken;
        }

        var account = await this._store.FindAccountByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (account is null || !CanReset(account))
        {
            await this._store.VoidTokensAsync(id, cancellationToken).ConfigureAwait(false);
            return PasswordResetOutcome.InvalidToken;
        }

        await this._store.UpdatePasswordHashAsync(id, BCrypt.HashPassword(newPassword!), cancellationToken).ConfigureAwait(false);
        await this._store.VoidTokensAsync(id, cancellationToken).ConfigureAwait(false);
        return PasswordResetOutcome.Changed;
    }

    private static bool CanReset(AccountRecord account) => account is { IsTemplate: false, IsBot: false, State: MUnique.OpenMU.DataModel.Entities.AccountState.Normal };

    // A token is 32 random bytes as 64 lowercase hex characters; only its SHA-256 is stored.
    private static bool TryHashToken(string? token, out string hash)
    {
        hash = string.Empty;
        if (token is not { Length: 64 } || !token.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
        {
            return false;
        }

        hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
        return true;
    }

    private async Task<string> IssueTokenAsync(Guid accountId, TokenPurpose purpose, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        TryHashToken(token, out var hash);
        await this._store.SetTokenAsync(accountId, purpose, hash, this._clock() + lifetime, cancellationToken).ConfigureAwait(false);
        return token;
    }
}
