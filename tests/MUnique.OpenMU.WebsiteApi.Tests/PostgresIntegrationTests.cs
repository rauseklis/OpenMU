// <copyright file="PostgresIntegrationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.WebsiteApi.Data;

/// <summary>
/// Runs the real store, and the HTTP API on top of it, against a disposable PostgreSQL database which holds the seeded game data
/// (see <see cref="WebsiteApiDatabase"/>). Skipped when <c>WEBSITE_API_TEST_CONNECTION_STRING</c> is not set.
/// </summary>
[TestFixture]
[Category("PostgreSql")]
public class PostgresIntegrationTests
{
    private PostgresWebsiteStore _store = null!;
    private ApiServer _api = null!;
    private NpgsqlDataSource _sql = null!;

    /// <summary>Prepares the database and the API.</summary>
    /// <returns>The task.</returns>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        await WebsiteApiDatabase.EnsureAsync();
        this._sql = NpgsqlDataSource.Create(WebsiteApiDatabase.ConnectionString!);
        this._store = new PostgresWebsiteStore(null);
        this._api = await ApiServer.StartAsync(this._store, new FakeStatus());
    }

    /// <summary>Stops the API.</summary>
    /// <returns>The task.</returns>
    [OneTimeTearDown]
    public async Task TearDownAsync()
    {
        if (this._api is not null)
        {
            await this._api.DisposeAsync();
        }

        if (this._store is not null)
        {
            await this._store.DisposeAsync();
        }

        if (this._sql is not null)
        {
            await this._sql.DisposeAsync();
        }
    }

    /// <summary>The stat ids in the store are the ones of the game.</summary>
    [Test]
    public void StatIdsAreTheOnesOfTheGame()
    {
        Assert.That(PostgresWebsiteStore.LevelId, Is.EqualTo(Stats.Level.Id));
        Assert.That(PostgresWebsiteStore.MasterLevelId, Is.EqualTo(Stats.MasterLevel.Id));
        Assert.That(PostgresWebsiteStore.ResetsId, Is.EqualTo(Stats.Resets.Id));
        Assert.That(PostgresWebsiteStore.StrengthId, Is.EqualTo(Stats.BaseStrength.Id));
        Assert.That(PostgresWebsiteStore.AgilityId, Is.EqualTo(Stats.BaseAgility.Id));
        Assert.That(PostgresWebsiteStore.VitalityId, Is.EqualTo(Stats.BaseVitality.Id));
        Assert.That(PostgresWebsiteStore.EnergyId, Is.EqualTo(Stats.BaseEnergy.Id));
        Assert.That(PostgresWebsiteStore.LeadershipId, Is.EqualTo(Stats.BaseLeadership.Id));
    }

    /// <summary>
    /// The ranking is ordered, numbered from 1, honours the limit and leaves out GM accounts and GM characters, banned,
    /// temporarily banned, template and bot accounts.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task PlayerRankingHidesGmsBannedTemplateAndBots()
    {
        var visibleBefore = (await this._store.GetPlayerRankingAsync(100, default)).Select(e => e.Name).ToHashSet();
        Assert.That(visibleBefore, Is.Not.Empty, "the seeded test accounts have characters");

        var gmCharacters = await this.CharacterNamesAsync("testgm", "testgm2", "testunlock");
        Assert.That(gmCharacters, Is.Not.Empty);
        Assert.That(visibleBefore.Intersect(gmCharacters), Is.Empty, "characters of GM accounts are hidden");

        var banned = await this.CharacterNamesAsync("test1");
        var tempBanned = await this.CharacterNamesAsync("test2");
        var template = await this.CharacterNamesAsync("test3");
        var bot = await this.CharacterNamesAsync("test4");
        var gmCharacter = (await this.CharacterNamesAsync("test5")).First();

        await this.ExecuteAsync("UPDATE data.\"Account\" SET \"State\" = 4 WHERE \"LoginName\" = 'test1'");
        await this.ExecuteAsync("UPDATE data.\"Account\" SET \"State\" = 5 WHERE \"LoginName\" = 'test2'");
        await this.ExecuteAsync("UPDATE data.\"Account\" SET \"IsTemplate\" = true WHERE \"LoginName\" = 'test3'");
        await this.ExecuteAsync("UPDATE data.\"Account\" SET \"IsBot\" = true WHERE \"LoginName\" = 'test4'");
        await this.ExecuteAsync($"UPDATE data.\"Character\" SET \"CharacterStatus\" = 32 WHERE \"Name\" = '{gmCharacter}'");
        try
        {
            var ranking = await this._store.GetPlayerRankingAsync(100, default);
            var names = ranking.Select(e => e.Name).ToHashSet();

            Assert.That(names.Intersect(banned), Is.Empty, "banned");
            Assert.That(names.Intersect(tempBanned), Is.Empty, "temporarily banned");
            Assert.That(names.Intersect(template), Is.Empty, "template");
            Assert.That(names.Intersect(bot), Is.Empty, "bot");
            Assert.That(names, Does.Not.Contain(gmCharacter), "GM character");
            Assert.That(names.Intersect(gmCharacters), Is.Empty);
            Assert.That(names, Is.Not.Empty);

            Assert.That(ranking.Select(e => e.Rank), Is.EqualTo(Enumerable.Range(1, ranking.Count)));
            Assert.That(ranking, Is.Ordered.Descending.By(nameof(PlayerRankEntry.Resets)).Then.Descending.By(nameof(PlayerRankEntry.MasterLevel)).Then.Descending.By(nameof(PlayerRankEntry.Level)));
            Assert.That(await this._store.GetPlayerRankingAsync(2, default), Has.Count.EqualTo(Math.Min(2, ranking.Count)));
        }
        finally
        {
            await this.ExecuteAsync("UPDATE data.\"Account\" SET \"State\" = 0, \"IsTemplate\" = false, \"IsBot\" = false WHERE \"LoginName\" IN ('test1', 'test2', 'test3', 'test4')");
            await this.ExecuteAsync($"UPDATE data.\"Character\" SET \"CharacterStatus\" = 0 WHERE \"Name\" = '{gmCharacter}'");
        }
    }

    /// <summary>
    /// A character profile shows the real class, level, resets and master level, and is not available for hidden characters.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CharacterProfileMatchesTheDatabase()
    {
        var name = (await this.CharacterNamesAsync("test5")).First();
        var level = await this.ScalarAsync($"SELECT COALESCE((SELECT s.\"Value\" FROM data.\"StatAttribute\" s JOIN data.\"Character\" c ON c.\"Id\" = s.\"CharacterId\" WHERE c.\"Name\" = '{name}' AND s.\"DefinitionId\" = '{Stats.Level.Id}'), 1)::int");
        var className = (string)(await this.ScalarAsync($"SELECT cc.\"Name\" FROM data.\"Character\" c JOIN config.\"CharacterClass\" cc ON cc.\"Id\" = c.\"CharacterClassId\" WHERE c.\"Name\" = '{name}'"))!;

        var profile = await this._store.GetCharacterAsync(name.ToUpperInvariant(), default);

        Assert.That(profile, Is.Not.Null);
        Assert.That(profile!.Name, Is.EqualTo(name));
        Assert.That(profile.Class, Is.EqualTo(className));
        Assert.That(profile.Level, Is.EqualTo(level));
        Assert.That(profile.Level, Is.GreaterThan(1));

        var gm = (await this.CharacterNamesAsync("testgm")).First();
        Assert.That(await this._store.GetCharacterAsync(gm, default), Is.Null);
        Assert.That(await this._store.GetCharacterAsync("NoSuchName", default), Is.Null);
        Assert.That(await this._store.GetCharacterAsync("a' OR '1'='1", default), Is.Null, "names are parameters, not SQL");
    }

    /// <summary>
    /// Guilds are ranked by score with their visible members, the master and the logo; GM members are not listed.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task GuildPagesAndRanking()
    {
        var visible = (await this.CharacterNamesAsync("test6")).First();
        var visible2 = (await this.CharacterNamesAsync("test7")).First();
        var gm = (await this.CharacterNamesAsync("testgm")).First();
        var guildId = Guid.NewGuid();
        var logo = new byte[32];
        logo[0] = 0x31;

        await using (var command = this._sql.CreateCommand("INSERT INTO guild.\"Guild\" (\"Id\", \"Name\", \"Score\", \"Logo\") VALUES (@id, 'WebTst', 777777, @logo)"))
        {
            command.Parameters.AddWithValue("id", guildId);
            command.Parameters.AddWithValue("logo", logo);
            await command.ExecuteNonQueryAsync();
        }

        await this.ExecuteAsync($"INSERT INTO guild.\"GuildMember\" (\"Id\", \"GuildId\", \"Status\") SELECT \"Id\", '{guildId}', {(int)GuildPosition.GuildMaster} FROM data.\"Character\" WHERE \"Name\" = '{visible}'");
        await this.ExecuteAsync($"INSERT INTO guild.\"GuildMember\" (\"Id\", \"GuildId\", \"Status\") SELECT \"Id\", '{guildId}', {(int)GuildPosition.NormalMember} FROM data.\"Character\" WHERE \"Name\" IN ('{visible2}', '{gm}')");
        try
        {
            var ranking = await this._store.GetGuildRankingAsync(10, default);
            var entry = ranking.Single(g => g.Name == "WebTst");
            Assert.That(entry.Rank, Is.EqualTo(1), "the highest score is first");
            Assert.That(entry.Score, Is.EqualTo(777777));
            Assert.That(entry.Members, Is.EqualTo(2), "the GM member is not counted");
            Assert.That(entry.Master, Is.EqualTo(visible));

            var profile = await this._store.GetGuildAsync("webtst", default);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.Master, Is.EqualTo(visible));
            Assert.That(profile.Members.Select(m => m.Name), Is.EquivalentTo(new[] { visible, visible2 }));
            Assert.That(profile.Members[0].Name, Is.EqualTo(visible), "the master comes first");
            Assert.That(profile.Logo, Is.EqualTo(logo));

            var character = await this._store.GetCharacterAsync(visible, default);
            Assert.That(character!.Guild, Is.EqualTo("WebTst"));

            var http = (await this._api.SendAsync("GET", "/api/v1/guilds/WebTst")).Json;
            Assert.That(http.GetProperty("logoPng").GetString(), Is.Not.Empty);
            Assert.That(http.GetProperty("members").GetArrayLength(), Is.EqualTo(2));
        }
        finally
        {
            await this.ExecuteAsync($"DELETE FROM guild.\"Guild\" WHERE \"Id\" = '{guildId}'");
        }

        Assert.That(await this._store.GetGuildAsync("WebTst", default), Is.Null);
    }

    /// <summary>
    /// A registration through the API creates a normal account which the game's own login check accepts
    /// (<see cref="IPlayerContext.AuthenticateAsync"/>, the call of the game login), and the password change works for the game too.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RegisteredAccountCanLogInToTheGame()
    {
        await this.ExecuteAsync("DELETE FROM data.\"Account\" WHERE \"LoginName\" = 'WebTst1'");
        var before = DateTime.UtcNow.AddSeconds(-5);

        var created = await this._api.SendAsync("POST", "/api/v1/accounts", new { loginName = "WebTst1", password = "game-pass-1", email = "webtst1@example.com" }, clientIp: "203.0.113.1");
        Assert.That(created.Status, Is.EqualTo(201), created.Body);

        await using (var command = this._sql.CreateCommand("SELECT \"Id\", \"PasswordHash\", \"State\", \"IsTemplate\", \"IsBot\", \"RegistrationDate\", \"EMail\", \"SecurityCode\" FROM data.\"Account\" WHERE \"LoginName\" = 'WebTst1'"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.That(await reader.ReadAsync(), Is.True);
            Assert.That(reader.GetGuid(0).ToString(), Is.EqualTo(created.Json.GetProperty("id").GetString()));
            Assert.That(reader.GetString(1), Does.StartWith("$2"));
            Assert.That(reader.GetInt32(2), Is.EqualTo((int)AccountState.Normal));
            Assert.That(reader.GetBoolean(3), Is.False);
            Assert.That(reader.GetBoolean(4), Is.False);
            Assert.That(reader.GetDateTime(5), Is.GreaterThan(before).And.LessThan(DateTime.UtcNow.AddSeconds(5)));
            Assert.That(reader.GetString(6), Is.EqualTo("webtst1@example.com"));
        }

        using var gameContext = await this.CreatePlayerContextAsync();
        Assert.That(await gameContext.AuthenticateAsync("WebTst1", "game-pass-1"), Is.EqualTo(AccountState.Normal));
        Assert.That(await gameContext.AuthenticateAsync("WebTst1", "wrong-pass-1"), Is.Null);

        var duplicate = await this._api.SendAsync("POST", "/api/v1/accounts", new { loginName = "webtst1", password = "game-pass-1", email = "other@example.com" }, clientIp: "203.0.113.1");
        Assert.That((duplicate.Status, duplicate.Error), Is.EqualTo((409, "name_taken")));
        var duplicateEmail = await this._api.SendAsync("POST", "/api/v1/accounts", new { loginName = "WebTst2", password = "game-pass-1", email = "WEBTST1@example.com" }, clientIp: "203.0.113.1");
        Assert.That((duplicateEmail.Status, duplicateEmail.Error), Is.EqualTo((409, "email_taken")));

        var changed = await this._api.SendAsync("POST", "/api/v1/accounts/password", new { loginName = "WebTst1", currentPassword = "game-pass-1", newPassword = "game-pass-2" }, clientIp: "203.0.113.1");
        Assert.That(changed.Status, Is.EqualTo(204));
        Assert.That(await gameContext.AuthenticateAsync("WebTst1", "game-pass-2"), Is.EqualTo(AccountState.Normal));
        Assert.That(await gameContext.AuthenticateAsync("WebTst1", "game-pass-1"), Is.Null);

        var verified = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "WebTst1", password = "game-pass-2" }, clientIp: "203.0.113.1");
        Assert.That(verified.Status, Is.EqualTo(200));
        var characters = await this._api.SendAsync("GET", "/api/v1/accounts/WebTst1/characters");
        Assert.That(characters.Json.GetArrayLength(), Is.EqualTo(0), "a new account has no characters");

        await this.ExecuteAsync("DELETE FROM data.\"Account\" WHERE \"LoginName\" = 'WebTst1'");
    }

    /// <summary>
    /// Several registrations of the same name at once create exactly one account.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ConcurrentRegistrationsOfOneNameCreateOneAccount()
    {
        await this.ExecuteAsync("DELETE FROM data.\"Account\" WHERE \"LoginName\" = 'WebRace'");
        var tasks = Enumerable.Range(0, 4)
            .Select(i => this._api.SendAsync("POST", "/api/v1/accounts", new { loginName = "WebRace", password = "race-pass-1", email = $"race{i}@example.com" }, clientIp: $"203.0.113.{20 + i}"))
            .ToList();
        var results = await Task.WhenAll(tasks);

        Assert.That(results.Count(r => r.Status == 201), Is.EqualTo(1));
        Assert.That(results.Count(r => r.Status == 409), Is.EqualTo(3));
        Assert.That(await this.ScalarAsync("SELECT count(*)::int FROM data.\"Account\" WHERE lower(\"LoginName\") = 'webrace'"), Is.EqualTo(1));
        await this.ExecuteAsync("DELETE FROM data.\"Account\" WHERE \"LoginName\" = 'WebRace'");
    }

    /// <summary>
    /// Seeded accounts (password = login name) verify with their real state; banned and template accounts get the generic 401.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task VerifyUsesTheRealAccountState()
    {
        var normal = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "test8", password = "test8" }, clientIp: "203.0.113.50");
        Assert.That(normal.Status, Is.EqualTo(200), normal.Body);
        Assert.That(normal.Json.GetProperty("state").GetString(), Is.EqualTo("Normal"));

        var gm = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "testgm", password = "testgm" }, clientIp: "203.0.113.51");
        if (gm.Status == 200)
        {
            Assert.That(gm.Json.GetProperty("state").GetString(), Is.EqualTo("GameMaster"));
        }

        await this.ExecuteAsync("UPDATE data.\"Account\" SET \"State\" = 4 WHERE \"LoginName\" = 'test9'");
        await this.ExecuteAsync("UPDATE data.\"Account\" SET \"IsTemplate\" = true WHERE \"LoginName\" = 'test0'");
        try
        {
            var banned = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "test9", password = "test9" }, clientIp: "203.0.113.52");
            var wrong = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "test9", password = "not-the-pw-1" }, clientIp: "203.0.113.53");
            var template = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "test0", password = "test0" }, clientIp: "203.0.113.54");
            Assert.That(banned.Status, Is.EqualTo(401));
            Assert.That(banned.Body, Is.EqualTo(wrong.Body));
            Assert.That(template.Body, Is.EqualTo(wrong.Body));
            Assert.That((await this._api.SendAsync("GET", "/api/v1/accounts/test9/characters")).Status, Is.EqualTo(404));
        }
        finally
        {
            await this.ExecuteAsync("UPDATE data.\"Account\" SET \"State\" = 0, \"IsTemplate\" = false WHERE \"LoginName\" IN ('test9', 'test0')");
        }
    }

    /// <summary>
    /// The character list of an account lists all its characters with level and resets, and the HTTP responses never contain a
    /// password hash or an e-mail address.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AccountCharactersAndNoLeaks()
    {
        await this.ExecuteAsync("UPDATE data.\"Account\" SET \"EMail\" = 'secret-mail@example.org' WHERE \"LoginName\" = 'test5'");
        try
        {
            var expected = await this.CharacterNamesAsync("test5");
            var list = await this._api.SendAsync("GET", "/api/v1/accounts/test5/characters");
            Assert.That(list.Json.EnumerateArray().Select(e => e.GetProperty("name").GetString()), Is.EquivalentTo(expected));

            var all = string.Concat(
                list.Body,
                (await this._api.SendAsync("GET", "/api/v1/rankings/players?limit=100")).Body,
                (await this._api.SendAsync("GET", "/api/v1/rankings/guilds?limit=100")).Body,
                (await this._api.SendAsync("GET", $"/api/v1/characters/{expected.First()}")).Body,
                (await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "test5", password = "test5" }, clientIp: "203.0.113.60")).Body);
            Assert.That(all, Does.Not.Contain("$2a$").And.Not.Contain("$2b$").And.Not.Contain("secret-mail").And.Not.Contain("SecurityCode").And.Not.Contain("passwordHash"));
        }
        finally
        {
            await this.ExecuteAsync("UPDATE data.\"Account\" SET \"EMail\" = '' WHERE \"LoginName\" = 'test5'");
        }
    }

    private async Task<IPlayerContext> CreatePlayerContextAsync()
    {
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>()).First();
        return provider.CreateNewPlayerContext(configuration);
    }

    private async Task<List<string>> CharacterNamesAsync(params string[] loginNames)
    {
        var result = new List<string>();
        await using var command = this._sql.CreateCommand("SELECT c.\"Name\" FROM data.\"Character\" c JOIN data.\"Account\" a ON a.\"Id\" = c.\"AccountId\" WHERE a.\"LoginName\" = ANY(@names) ORDER BY c.\"CharacterSlot\"");
        command.Parameters.AddWithValue("names", loginNames);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    /// <summary>
    /// The e-mail and reset flows against the real database: the tables of the API are created on first use, tokens are
    /// stored hashed, single use, and the new password works in the game.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AccountMailFlowsWorkOnPostgreSql()
    {
        await this.ExecuteAsync("DELETE FROM data.\"Account\" WHERE \"LoginName\" IN ('MailTst1', 'MailTst2')");
        await this.ExecuteAsync("DROP SCHEMA IF EXISTS website CASCADE");
        var ip = "203.0.113.50";

        Assert.That((await this._api.SendAsync("POST", "/api/v1/accounts", new { loginName = "MailTst1", password = "game-pass-1", email = "mailtst1@example.com" }, clientIp: ip)).Status, Is.EqualTo(201));

        // The first access creates the tables.
        var page = (await this._api.SendAsync("GET", "/api/v1/accounts/MailTst1")).Json;
        Assert.That(page.GetProperty("email").GetString(), Is.EqualTo("mailtst1@example.com"));
        Assert.That(page.GetProperty("emailVerified").GetBoolean(), Is.False);
        Assert.That(page.GetProperty("registeredAt").GetDateTime(), Is.GreaterThan(DateTime.UtcNow.AddMinutes(-2)).And.LessThan(DateTime.UtcNow.AddMinutes(2)));

        var verification = await this._api.SendAsync("POST", "/api/v1/accounts/verify-email/request", new { loginName = "MailTst1" }, clientIp: ip);
        Assert.That(verification.Status, Is.EqualTo(200), verification.Body);
        var verifyToken = verification.Json.GetProperty("token").GetString()!;
        Assert.That(await this.ScalarAsync("SELECT count(*) FROM website.token WHERE token_hash = '" + verifyToken + "'"), Is.EqualTo(0L), "the token itself is not stored");
        Assert.That(await this.ScalarAsync("SELECT count(*) FROM website.token"), Is.EqualTo(1L));

        var confirmed = await this._api.SendAsync("POST", "/api/v1/accounts/verify-email/confirm", new { token = verifyToken }, clientIp: ip);
        Assert.That(confirmed.Json.GetProperty("loginName").GetString(), Is.EqualTo("MailTst1"));
        Assert.That((await this._api.SendAsync("GET", "/api/v1/accounts/MailTst1")).Json.GetProperty("emailVerified").GetBoolean(), Is.True);
        Assert.That(await this.ScalarAsync("SELECT count(*) FROM website.token"), Is.EqualTo(0L), "single use");

        // Changing the address un-verifies it.
        var change = await this._api.SendAsync("POST", "/api/v1/accounts/email", new { loginName = "MailTst1", password = "game-pass-1", newEmail = "mailtst2@example.com" }, clientIp: ip);
        Assert.That(change.Status, Is.EqualTo(204), change.Body);
        var changedPage = (await this._api.SendAsync("GET", "/api/v1/accounts/MailTst1")).Json;
        Assert.That(changedPage.GetProperty("email").GetString(), Is.EqualTo("mailtst2@example.com"));
        Assert.That(changedPage.GetProperty("emailVerified").GetBoolean(), Is.False);

        // Reset, then log in to the game with the new password.
        var reset = await this._api.SendAsync("POST", "/api/v1/accounts/reset/request", new { email = "MAILTST2@example.com" }, clientIp: ip);
        Assert.That(reset.Json.GetProperty("found").GetBoolean(), Is.True, reset.Body);
        Assert.That(reset.Json.GetProperty("loginName").GetString(), Is.EqualTo("MailTst1"));
        var resetToken = reset.Json.GetProperty("token").GetString()!;
        var done = await this._api.SendAsync("POST", "/api/v1/accounts/reset/confirm", new { token = resetToken, newPassword = "game-pass-3" }, clientIp: ip);
        Assert.That(done.Status, Is.EqualTo(204), done.Body);
        Assert.That((await this._api.SendAsync("POST", "/api/v1/accounts/reset/confirm", new { token = resetToken, newPassword = "game-pass-4" }, clientIp: ip)).Error, Is.EqualTo("invalid_token"));

        using var gameContext = await this.CreatePlayerContextAsync();
        Assert.That(await gameContext.AuthenticateAsync("MailTst1", "game-pass-3"), Is.EqualTo(AccountState.Normal));
        Assert.That(await gameContext.AuthenticateAsync("MailTst1", "game-pass-1"), Is.Null);

        // Deleting the account removes its rows in the API tables (foreign key with cascade).
        await this._api.SendAsync("POST", "/api/v1/accounts/reset/request", new { email = "mailtst2@example.com" }, clientIp: "203.0.113.51");
        Assert.That(await this.ScalarAsync("SELECT count(*) FROM website.token"), Is.EqualTo(1L));
        await this.ExecuteAsync("DELETE FROM data.\"Account\" WHERE \"LoginName\" = 'MailTst1'");
        Assert.That(await this.ScalarAsync("SELECT count(*) FROM website.token"), Is.EqualTo(0L));
    }

    /// <summary>
    /// Batch A against the real database: killers ranking, online list, bans, search, castle siege and the extended character list.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task BatchAQueriesWorkOnPostgreSql()
    {
        // The seeded test accounts have characters; give one of them kills and a banned neighbour.
        await using var names = this._sql.CreateCommand("SELECT c.\"Name\" FROM data.\"Character\" c JOIN data.\"Account\" a ON a.\"Id\" = c.\"AccountId\" WHERE a.\"IsTemplate\" = false AND a.\"IsBot\" = false AND a.\"State\" = 0 AND c.\"CharacterStatus\" = 0 ORDER BY c.\"Name\" LIMIT 2");
        var characterNames = new List<string>();
        await using (var reader = await names.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                characterNames.Add(reader.GetString(0));
            }
        }

        Assert.That(characterNames, Is.Not.Empty, "the seeded data has characters of normal accounts");
        var hero = characterNames[0];
        await this.ExecuteAsync($"UPDATE data.\"Character\" SET \"PlayerKillCount\" = 42, \"State\" = 4 WHERE \"Name\" = '{hero}'");

        var killers = (await this._api.SendAsync("GET", "/api/v1/rankings/killers?limit=5")).Json;
        Assert.That(killers[0].GetProperty("name").GetString(), Is.EqualTo(hero));
        Assert.That(killers[0].GetProperty("value").GetInt32(), Is.EqualTo(42));
        Assert.That(killers[0].GetProperty("rank").GetInt32(), Is.EqualTo(1));
        Assert.That(killers[0].EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "rank", "name", "class", "value", "guild" }));

        // /search: contains, ignoring the case; wildcards typed by the user do not match everything.
        var found = (await this._api.SendAsync("GET", "/api/v1/search?q=" + hero[..2].ToUpperInvariant())).Json;
        Assert.That(found.GetProperty("characters").EnumerateArray().Select(e => e.GetString()), Does.Contain(hero));
        Assert.That((await this._api.SendAsync("GET", "/api/v1/search?q=%25%25")).Json.GetProperty("characters").GetArrayLength(), Is.EqualTo(0));
        Assert.That((await this._api.SendAsync("GET", "/api/v1/search?q=a")).Error, Is.EqualTo("invalid_input"));

        // /bans: character names of banned characters and of banned accounts, nothing else.
        if (characterNames.Count > 1)
        {
            await this.ExecuteAsync($"UPDATE data.\"Character\" SET \"CharacterStatus\" = 1 WHERE \"Name\" = '{characterNames[1]}'");
            var bans = (await this._api.SendAsync("GET", "/api/v1/bans")).Json;
            var banned = bans.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();
            Assert.That(banned, Does.Contain(characterNames[1]));
            Assert.That(banned, Does.Not.Contain(hero));
            Assert.That(bans[0].GetProperty("reason").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
            Assert.That((await this._api.SendAsync("GET", "/api/v1/rankings/killers")).Body, Does.Not.Contain(characterNames[1]).Or.Contain(hero));
            await this.ExecuteAsync($"UPDATE data.\"Character\" SET \"CharacterStatus\" = 0 WHERE \"Name\" = '{characterNames[1]}'");
        }

        // /online uses the names of the running server (here: the fake status source is empty, so nothing).
        Assert.That((await this._api.SendAsync("GET", "/api/v1/online")).Json.GetArrayLength(), Is.EqualTo(0));

        // /castle-siege: the tables are read; a fresh database has no owner and no registrations.
        var siege = (await this._api.SendAsync("GET", "/api/v1/castle-siege")).Json;
        Assert.That(siege.GetProperty("owner").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
        Assert.That(siege.GetProperty("nextSiege").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
        Assert.That(siege.GetProperty("registered").GetArrayLength(), Is.EqualTo(0));

        // Extended character list of an account.
        await using var owner = this._sql.CreateCommand("SELECT a.\"LoginName\" FROM data.\"Account\" a JOIN data.\"Character\" c ON c.\"AccountId\" = a.\"Id\" WHERE c.\"Name\" = @n");
        owner.Parameters.AddWithValue("n", hero);
        var login = (string)(await owner.ExecuteScalarAsync())!;
        var list = (await this._api.SendAsync("GET", "/api/v1/accounts/" + login + "/characters")).Json;
        var item = list.EnumerateArray().First(e => e.GetProperty("name").GetString() == hero);
        Assert.That(item.GetProperty("pk").GetInt32(), Is.EqualTo(42));
        Assert.That(item.GetProperty("pkState").GetString(), Is.EqualTo("PlayerKillWarning"));
        Assert.That(item.GetProperty("stats").GetProperty("str").GetInt32(), Is.GreaterThan(0), "base strength comes from the stat attributes");
        Assert.That(item.GetProperty("points").GetInt32(), Is.GreaterThanOrEqualTo(0));
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = this._sql.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var command = this._sql.CreateCommand(sql);
        return await command.ExecuteScalarAsync();
    }
}
