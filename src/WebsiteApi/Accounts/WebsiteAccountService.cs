// <copyright file="WebsiteAccountService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Accounts;

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

/// <summary>
/// The account logic of the website API: registration, credential check and password change. Hashing is
/// the same BCrypt call the game uses for its accounts (<c>BCrypt.Net.BCrypt.HashPassword</c>, verified with
/// <c>BCrypt.Verify</c>, like <c>AccountRepository.AuthenticateAsync</c>), so an account made here can log in to the game.
/// </summary>
public sealed class WebsiteAccountService
{
    private static readonly string DummyHash = BCrypt.HashPassword("not-a-real-password-for-timing-only");

    private readonly IWebsiteStore _store;
    private readonly SemaphoreSlim _registrationLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="WebsiteAccountService"/> class.
    /// </summary>
    /// <param name="store">The data store.</param>
    public WebsiteAccountService(IWebsiteStore store)
    {
        this._store = store;
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
}
