// <copyright file="IWebsiteStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Data;

/// <summary>
/// The data access of the website API. Everything except <see cref="CreateAccountAsync"/> and
/// <see cref="UpdatePasswordHashAsync"/> only reads.
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
