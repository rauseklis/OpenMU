// <copyright file="IWebsiteStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Data;

/// <summary>
/// The data access of the website API. The methods which create or change an account, an
/// e-mail address or a token write; all others only read.
/// </summary>
public interface IWebsiteStore
{
    /// <summary>Gets the player ranking; GMs, template, bot and banned accounts are left out.</summary>
    /// <param name="limit">The maximum number of entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ranking.</returns>
    Task<IReadOnlyList<PlayerRankEntry>> GetPlayerRankingAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Gets the guild ranking.</summary>
    /// <param name="limit">The maximum number of entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ranking.</returns>
    Task<IReadOnlyList<GuildRankEntry>> GetGuildRankingAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Gets the public profile of a character; <c>null</c> if it does not exist or is hidden.</summary>
    /// <param name="name">The character name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profile.</returns>
    Task<CharacterProfile?> GetCharacterAsync(string name, CancellationToken cancellationToken);

    /// <summary>Gets the public profile of a guild; <c>null</c> if it does not exist.</summary>
    /// <param name="name">The guild name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profile.</returns>
    Task<GuildProfile?> GetGuildAsync(string name, CancellationToken cancellationToken);

    /// <summary>Gets all characters of an account.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The characters.</returns>
    Task<IReadOnlyList<AccountCharacter>> GetAccountCharactersAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Finds an account by its exact login name (the game compares exactly, too).</summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The record, or <c>null</c>.</returns>
    Task<AccountRecord?> FindAccountAsync(string loginName, CancellationToken cancellationToken);

    /// <summary>Tells whether a login name is used, ignoring the case.</summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if used.</returns>
    Task<bool> LoginNameExistsAsync(string loginName, CancellationToken cancellationToken);

    /// <summary>Tells whether an e-mail address is used, ignoring the case.</summary>
    /// <param name="email">The e-mail address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if used.</returns>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    /// <summary>Creates a normal account.</summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="passwordHash">The BCrypt hash.</param>
    /// <param name="email">The e-mail address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome and, if created, the id.</returns>
    Task<(CreateAccountOutcome Outcome, Guid Id)> CreateAccountAsync(string loginName, string passwordHash, string email, CancellationToken cancellationToken);

    /// <summary>Replaces the password hash of an account.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="passwordHash">The new BCrypt hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if the account was found and changed.</returns>
    Task<bool> UpdatePasswordHashAsync(Guid accountId, string passwordHash, CancellationToken cancellationToken);

    /// <summary>Finds an account by its id.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The record, or <c>null</c>.</returns>
    Task<AccountRecord?> FindAccountByIdAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Finds all accounts with an e-mail address, ignoring the case.</summary>
    /// <param name="email">The e-mail address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accounts (normally none or one).</returns>
    Task<IReadOnlyList<AccountRecord>> FindAccountsByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Gets the e-mail address, its verified flag and the registration time of an account.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The details, or <c>null</c> if the account does not exist.</returns>
    Task<AccountDetails?> GetAccountDetailsAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Sets a new e-mail address, marks it as not verified and voids all open tokens of the account.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="email">The new address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    Task UpdateEmailAsync(Guid accountId, string email, CancellationToken cancellationToken);

    /// <summary>Marks the e-mail address of an account as verified.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    Task SetEmailVerifiedAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Stores a token (by its hash) for an account and purpose, replacing an older one of the same purpose.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="tokenHash">The SHA-256 hash of the token.</param>
    /// <param name="expiresAt">When the token stops working.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    Task SetTokenAsync(Guid accountId, TokenPurpose purpose, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>Looks up the account of a valid (known, not expired) token without using it up.</summary>
    /// <param name="purpose">The purpose.</param>
    /// <param name="tokenHash">The SHA-256 hash of the token.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account id, or <c>null</c>.</returns>
    Task<Guid?> FindTokenAccountAsync(TokenPurpose purpose, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Uses up a valid token: atomically removes it and returns its account.</summary>
    /// <param name="purpose">The purpose.</param>
    /// <param name="tokenHash">The SHA-256 hash of the token.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account id, or <c>null</c> if the token is unknown, used or expired.</returns>
    Task<Guid?> ConsumeTokenAsync(TokenPurpose purpose, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Voids all open tokens of an account.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    Task VoidTokensAsync(Guid accountId, CancellationToken cancellationToken);
}

/// <summary>
/// Tells the state of the running game servers; implemented by the host.
/// </summary>
public interface IWebsiteStatusSource
{
    /// <summary>Gets the current status.</summary>
    /// <returns>The status.</returns>
    ServerStatus GetStatus();

    /// <summary>Tells whether a character is currently online.</summary>
    /// <param name="characterName">The character name.</param>
    /// <returns><c>true</c> if online.</returns>
    bool IsCharacterOnline(string characterName);
}
