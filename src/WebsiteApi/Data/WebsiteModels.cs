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

/// <summary>The base stats of a character.</summary>
/// <param name="Str">Strength.</param>
/// <param name="Agi">Agility.</param>
/// <param name="Vit">Vitality.</param>
/// <param name="Ene">Energy.</param>
/// <param name="Cmd">Command (leadership).</param>
public sealed record CharacterStats(int Str, int Agi, int Vit, int Ene, int Cmd);

/// <summary>A character of the account's own list.</summary>
/// <param name="Name">The character name.</param>
/// <param name="Class">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="Resets">The number of resets.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Stats">The base stats.</param>
/// <param name="Points">The unspent level-up points.</param>
/// <param name="PkKills">The number of killed players.</param>
/// <param name="PkState">The player killer state (Normal, PlayerKillWarning, ...).</param>
public sealed record AccountCharacter(string Name, string Class, int Level, int Resets, int MasterLevel, CharacterStats Stats, int Points, int PkKills, string PkState);

/// <summary>An entry of a ranking by a single value (for example the player kills).</summary>
/// <param name="Rank">The rank, starting with 1.</param>
/// <param name="Name">The character name.</param>
/// <param name="Class">The name of the character class.</param>
/// <param name="Value">The value which orders the ranking.</param>
/// <param name="Guild">The guild name, or <c>null</c>.</param>
public sealed record ValueRankEntry(int Rank, string Name, string Class, long Value, string? Guild);

/// <summary>A character which is online.</summary>
/// <param name="Name">The character name.</param>
/// <param name="Class">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="Guild">The guild name, or <c>null</c>.</param>
public sealed record OnlineCharacter(string Name, string Class, int Level, string? Guild);

/// <summary>The result of a search.</summary>
/// <param name="Characters">Matching visible character names.</param>
/// <param name="Guilds">Matching guild names.</param>
public sealed record SearchResult(IReadOnlyList<string> Characters, IReadOnlyList<string> Guilds);

/// <summary>A guild with its master, for the castle siege.</summary>
/// <param name="Guild">The guild name.</param>
/// <param name="Master">The guild master, or <c>null</c>.</param>
public sealed record SiegeGuild(string Guild, string? Master);

/// <summary>The castle siege state which the database knows.</summary>
/// <param name="Owner">The guild which owns the castle, or <c>null</c>.</param>
/// <param name="Registered">The guilds registered for the next siege.</param>
public sealed record CastleSiegeInfo(SiegeGuild? Owner, IReadOnlyList<SiegeGuild> Registered);

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

/// <summary>The status of one game server (a "realm" of the website).</summary>
/// <param name="Id">The server id (the <c>ServerID</c> of the game server definition).</param>
/// <param name="Name">The description of the game server as configured.</param>
/// <param name="Online">Whether this server is started.</param>
/// <param name="Players">The number of connected players.</param>
/// <param name="MaxPlayers">The number of players this server can hold.</param>
public sealed record RealmStatus(int Id, string Name, bool Online, int Players, int MaxPlayers);

/// <summary>The status of the game servers.</summary>
/// <param name="Online">Whether at least one game server is started.</param>
/// <param name="Players">The number of connected players.</param>
/// <param name="MaxPlayers">The number of players the started servers can hold.</param>
/// <param name="UptimeSince">When the server process started.</param>
/// <param name="Realms">The single game servers.</param>
public sealed record ServerStatus(bool Online, int Players, int MaxPlayers, DateTimeOffset UptimeSince, IReadOnlyList<RealmStatus> Realms);


/// <summary>The e-mail data of an account, for the account page of the website.</summary>
/// <param name="Email">The e-mail address (may be empty for old accounts).</param>
/// <param name="EmailVerified">Whether the owner confirmed the address.</param>
/// <param name="RegisteredAt">When the account was created (UTC).</param>
public sealed record AccountDetails(string Email, bool EmailVerified, DateTime RegisteredAt);

/// <summary>What a single-use token is for.</summary>
public enum TokenPurpose : short
{
    /// <summary>Confirms the e-mail address of an account.</summary>
    VerifyEmail = 1,

    /// <summary>Allows setting a new password.</summary>
    ResetPassword = 2,
}
