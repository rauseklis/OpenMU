// <copyright file="WebsiteModels.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Data;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>An entry of the player ranking.</summary>
/// <param name="Rank">The rank, starting with 1.</param>
/// <param name="Name">The character name.</param>
/// <param name="Class">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="Resets">The number of resets.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Guild">The guild name, or <c>null</c>.</param>
public sealed record PlayerRankEntry(int Rank, string Name, string Class, int Level, int Resets, int MasterLevel, string? Guild);

/// <summary>An entry of the guild ranking.</summary>
/// <param name="Rank">The rank, starting with 1.</param>
/// <param name="Name">The guild name.</param>
/// <param name="Score">The guild score.</param>
/// <param name="Members">The number of (visible) members.</param>
/// <param name="Master">The name of the guild master, or <c>null</c>.</param>
public sealed record GuildRankEntry(int Rank, string Name, int Score, int Members, string? Master);

/// <summary>The public profile of a character.</summary>
/// <param name="Name">The character name.</param>
/// <param name="Class">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="Resets">The number of resets.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Guild">The guild name, or <c>null</c>.</param>
public sealed record CharacterProfile(string Name, string Class, int Level, int Resets, int MasterLevel, string? Guild);

/// <summary>A member of a guild.</summary>
/// <param name="Name">The character name.</param>
/// <param name="Level">The level.</param>
public sealed record GuildMemberEntry(string Name, int Level);

/// <summary>The public profile of a guild.</summary>
/// <param name="Name">The guild name.</param>
/// <param name="Score">The score.</param>
/// <param name="Master">The guild master, or <c>null</c>.</param>
/// <param name="Members">The visible members.</param>
/// <param name="Logo">The raw logo (32 bytes, two 4-bit pixels per byte), or <c>null</c>.</param>
public sealed record GuildProfile(string Name, int Score, string? Master, IReadOnlyList<GuildMemberEntry> Members, byte[]? Logo);

/// <summary>A character of the account's own list.</summary>
/// <param name="Name">The character name.</param>
/// <param name="Class">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="Resets">The number of resets.</param>
public sealed record AccountCharacter(string Name, string Class, int Level, int Resets);

/// <summary>What the API needs to know to check a login. Never leaves the server.</summary>
/// <param name="Id">The account id.</param>
/// <param name="LoginName">The login name.</param>
/// <param name="PasswordHash">The BCrypt hash.</param>
/// <param name="State">The account state.</param>
/// <param name="IsTemplate">Whether the account is a template account.</param>
/// <param name="IsBot">Whether the account belongs to a server-side bot.</param>
public sealed record AccountRecord(Guid Id, string LoginName, string PasswordHash, AccountState State, bool IsTemplate, bool IsBot)
{
    /// <summary>
    /// Gets a value indicating whether somebody may log in with this account on the website:
    /// not banned (permanently or temporarily), not a template and not a bot.
    /// </summary>
    public bool CanLogIn => !this.IsTemplate && !this.IsBot && this.State is not (AccountState.Banned or AccountState.TemporarilyBanned);
}

/// <summary>The outcome of creating an account.</summary>
public enum CreateAccountOutcome
{
    /// <summary>The account was created.</summary>
    Created,

    /// <summary>The login name is already used.</summary>
    NameTaken,

    /// <summary>The e-mail address is already used.</summary>
    EmailTaken,
}

/// <summary>The status of the game servers.</summary>
/// <param name="Online">Whether at least one game server is started.</param>
/// <param name="Players">The number of connected players.</param>
/// <param name="MaxPlayers">The number of players the started servers can hold.</param>
/// <param name="UptimeSince">When the server process started.</param>
public sealed record ServerStatus(bool Online, int Players, int MaxPlayers, DateTimeOffset UptimeSince);
