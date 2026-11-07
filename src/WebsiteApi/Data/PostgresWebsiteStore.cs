// <copyright file="PostgresWebsiteStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Data;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence.EntityFramework;

/// <summary>
/// The PostgreSQL implementation of <see cref="IWebsiteStore"/>. All reads are plain SELECT statements on a
/// connection which can be a read-only role (<see cref="WebsiteApiOptions.ReadConnectionString"/>); the only
/// writes are the creation of an account and the replacement of a password hash, which go through the
/// normal Entity Framework context of the server.
/// </summary>
public sealed class PostgresWebsiteStore : IWebsiteStore, IAsyncDisposable
{
    // The ids of the stat attribute definitions (see GameLogic.Attributes.Stats; a test pins them).
    internal static readonly Guid LevelId = new("560931AD-0901-4342-B7F4-FD2E2FCC0563");
    internal static readonly Guid MasterLevelId = new("70CD8C10-391A-4C51-9AA4-A854600E3A9F");
    internal static readonly Guid ResetsId = new("89A891A7-F9F9-4AB5-AF36-12056E53A5F7");

    // Public lists show normal players only: no GMs (account or character), no banned, template or bot accounts.
    private const string VisibleCharacter = "a.\"IsTemplate\" = false AND a.\"IsBot\" = false AND a.\"State\" = 0 AND c.\"CharacterStatus\" = 0";

    private const string StatJoins = @"
        LEFT JOIN data.""StatAttribute"" lv ON lv.""CharacterId"" = c.""Id"" AND lv.""DefinitionId"" = @level
        LEFT JOIN data.""StatAttribute"" rs ON rs.""CharacterId"" = c.""Id"" AND rs.""DefinitionId"" = @resets
        LEFT JOIN data.""StatAttribute"" ml ON ml.""CharacterId"" = c.""Id"" AND ml.""DefinitionId"" = @master";

    private const string PlayerColumns = @"
        c.""Name"", cc.""Name"" AS class, COALESCE(lv.""Value"", 1)::int AS level, COALESCE(rs.""Value"", 0)::int AS resets,
        COALESCE(ml.""Value"", 0)::int AS master, g.""Name"" AS guild";

    private const string PlayerJoins = @"
        FROM data.""Character"" c
        JOIN data.""Account"" a ON a.""Id"" = c.""AccountId""
        JOIN config.""CharacterClass"" cc ON cc.""Id"" = c.""CharacterClassId""" + StatJoins + @"
        LEFT JOIN guild.""GuildMember"" gm ON gm.""Id"" = c.""Id""
        LEFT JOIN guild.""Guild"" g ON g.""Id"" = gm.""GuildId""";

    private readonly string? _readConnectionString;
    private NpgsqlDataSource? _dataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresWebsiteStore"/> class.
    /// </summary>
    /// <param name="readConnectionString">The connection string for reads; <c>null</c> to use the connection of the server itself.</param>
    public PostgresWebsiteStore(string? readConnectionString)
    {
        this._readConnectionString = readConnectionString;
    }

    private NpgsqlDataSource DataSource
    {
        get
        {
            if (this._dataSource is null)
            {
                var connectionString = this._readConnectionString ?? ConnectionConfigurator.GetConnectionString<EntityDataContext>();
                var builder = new NpgsqlConnectionStringBuilder(connectionString) { ApplicationName = "openmu-website-api" };
                this._dataSource = NpgsqlDataSource.Create(builder.ConnectionString);
            }

            return this._dataSource;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlayerRankEntry>> GetPlayerRankingAsync(int limit, CancellationToken cancellationToken)
    {
        var sql = "SELECT" + PlayerColumns + PlayerJoins + $@"
            WHERE {VisibleCharacter}
            ORDER BY resets DESC, master DESC, level DESC, c.""Experience"" DESC, c.""Name""
            LIMIT @limit";

        var result = new List<PlayerRankEntry>();
        await using var command = this.DataSource.CreateCommand(sql);
        AddStatParameters(command);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new PlayerRankEntry(result.Count + 1, reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), await GetNullableStringAsync(reader, 5, cancellationToken).ConfigureAwait(false)));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GuildRankEntry>> GetGuildRankingAsync(int limit, CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT g.""Name"", g.""Score"", COUNT(c.""Id"")::int AS members,
                (SELECT mc.""Name"" FROM guild.""GuildMember"" mm
                    JOIN data.""Character"" mc ON mc.""Id"" = mm.""Id""
                    JOIN data.""Account"" ma ON ma.""Id"" = mc.""AccountId""
                    WHERE mm.""GuildId"" = g.""Id"" AND mm.""Status"" = {(int)GuildPosition.GuildMaster}
                      AND ma.""IsTemplate"" = false AND ma.""IsBot"" = false AND ma.""State"" = 0 AND mc.""CharacterStatus"" = 0
                    LIMIT 1) AS master
            FROM guild.""Guild"" g
            JOIN guild.""GuildMember"" gm ON gm.""GuildId"" = g.""Id""
            JOIN data.""Character"" c ON c.""Id"" = gm.""Id""
            JOIN data.""Account"" a ON a.""Id"" = c.""AccountId""
            WHERE {VisibleCharacter}
            GROUP BY g.""Id"", g.""Name"", g.""Score""
            ORDER BY g.""Score"" DESC, members DESC, g.""Name""
            LIMIT @limit";

        var result = new List<GuildRankEntry>();
        await using var command = this.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new GuildRankEntry(result.Count + 1, reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), await GetNullableStringAsync(reader, 3, cancellationToken).ConfigureAwait(false)));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<CharacterProfile?> GetCharacterAsync(string name, CancellationToken cancellationToken)
    {
        var sql = "SELECT" + PlayerColumns + PlayerJoins + $@"
            WHERE {VisibleCharacter} AND lower(c.""Name"") = lower(@name)
            ORDER BY (c.""Name"" = @name) DESC
            LIMIT 1";

        await using var command = this.DataSource.CreateCommand(sql);
        AddStatParameters(command);
        command.Parameters.AddWithValue("name", name);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new CharacterProfile(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), await GetNullableStringAsync(reader, 5, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<GuildProfile?> GetGuildAsync(string name, CancellationToken cancellationToken)
    {
        await using var connection = await this.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        Guid guildId;
        string guildName;
        int score;
        byte[]? logo;
        await using (var command = new NpgsqlCommand(@"SELECT ""Id"", ""Name"", ""Score"", ""Logo"" FROM guild.""Guild"" WHERE lower(""Name"") = lower(@name) ORDER BY (""Name"" = @name) DESC LIMIT 1", connection))
        {
            command.Parameters.AddWithValue("name", name);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            guildId = reader.GetGuid(0);
            guildName = reader.GetString(1);
            score = reader.GetInt32(2);
            logo = await reader.IsDBNullAsync(3, cancellationToken).ConfigureAwait(false) ? null : (byte[])reader[3];
        }

        var members = new List<GuildMemberEntry>();
        string? master = null;
        var sql = $@"
            SELECT c.""Name"", COALESCE(lv.""Value"", 1)::int, gm.""Status""
            FROM guild.""GuildMember"" gm
            JOIN data.""Character"" c ON c.""Id"" = gm.""Id""
            JOIN data.""Account"" a ON a.""Id"" = c.""AccountId""
            LEFT JOIN data.""StatAttribute"" lv ON lv.""CharacterId"" = c.""Id"" AND lv.""DefinitionId"" = @level
            WHERE gm.""GuildId"" = @guild AND {VisibleCharacter}
            ORDER BY gm.""Status"" DESC, COALESCE(lv.""Value"", 1) DESC, c.""Name""";
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("guild", guildId);
            command.Parameters.AddWithValue("level", LevelId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var memberName = reader.GetString(0);
                members.Add(new GuildMemberEntry(memberName, reader.GetInt32(1)));
                if (master is null && reader.GetInt16(2) == (short)GuildPosition.GuildMaster)
                {
                    master = memberName;
                }
            }
        }

        return new GuildProfile(guildName, score, master, members, logo);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountCharacter>> GetAccountCharactersAsync(Guid accountId, CancellationToken cancellationToken)
    {
        const string Sql = @"
            SELECT c.""Name"", cc.""Name"", COALESCE(lv.""Value"", 1)::int, COALESCE(rs.""Value"", 0)::int
            FROM data.""Character"" c
            JOIN config.""CharacterClass"" cc ON cc.""Id"" = c.""CharacterClassId""
            LEFT JOIN data.""StatAttribute"" lv ON lv.""CharacterId"" = c.""Id"" AND lv.""DefinitionId"" = @level
            LEFT JOIN data.""StatAttribute"" rs ON rs.""CharacterId"" = c.""Id"" AND rs.""DefinitionId"" = @resets
            WHERE c.""AccountId"" = @account
            ORDER BY c.""CharacterSlot""";

        var result = new List<AccountCharacter>();
        await using var command = this.DataSource.CreateCommand(Sql);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("level", LevelId);
        command.Parameters.AddWithValue("resets", ResetsId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new AccountCharacter(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3)));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AccountRecord?> FindAccountAsync(string loginName, CancellationToken cancellationToken)
    {
        await using var command = this.DataSource.CreateCommand(@"SELECT ""Id"", ""LoginName"", ""PasswordHash"", ""State"", ""IsTemplate"", ""IsBot"" FROM data.""Account"" WHERE ""LoginName"" = @name");
        command.Parameters.AddWithValue("name", loginName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new AccountRecord(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), (AccountState)reader.GetInt32(3), reader.GetBoolean(4), reader.GetBoolean(5));
    }

    /// <inheritdoc />
    public async Task<bool> LoginNameExistsAsync(string loginName, CancellationToken cancellationToken)
    {
        await using var command = this.DataSource.CreateCommand(@"SELECT EXISTS (SELECT 1 FROM data.""Account"" WHERE lower(""LoginName"") = lower(@name))");
        command.Parameters.AddWithValue("name", loginName);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        await using var command = this.DataSource.CreateCommand(@"SELECT EXISTS (SELECT 1 FROM data.""Account"" WHERE ""EMail"" <> '' AND lower(""EMail"") = lower(@email))");
        command.Parameters.AddWithValue("email", email);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<(CreateAccountOutcome Outcome, Guid Id)> CreateAccountAsync(string loginName, string passwordHash, string email, CancellationToken cancellationToken)
    {
        await using var context = new EntityDataContext();
        var account = new Persistence.EntityFramework.Model.Account
        {
            LoginName = loginName,
            PasswordHash = passwordHash,
            EMail = email,
            RegistrationDate = DateTime.UtcNow,
            State = AccountState.Normal,
        };

        context.Add(account);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return (CreateAccountOutcome.NameTaken, Guid.Empty);
        }

        return (CreateAccountOutcome.Created, account.Id);
    }

    /// <inheritdoc />
    public async Task<bool> UpdatePasswordHashAsync(Guid accountId, string passwordHash, CancellationToken cancellationToken)
    {
        await using var context = new EntityDataContext();
        var changed = await context.Set<Persistence.EntityFramework.Model.Account>()
            .Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PasswordHash, passwordHash), cancellationToken)
            .ConfigureAwait(false);
        return changed > 0;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this._dataSource is { } dataSource)
        {
            await dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<string?> GetNullableStringAsync(NpgsqlDataReader reader, int index, CancellationToken cancellationToken)
    {
        return await reader.IsDBNullAsync(index, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(index);
    }

    private static void AddStatParameters(NpgsqlCommand command)
    {
        command.Parameters.AddWithValue("level", LevelId);
        command.Parameters.AddWithValue("resets", ResetsId);
        command.Parameters.AddWithValue("master", MasterLevelId);
    }
}
