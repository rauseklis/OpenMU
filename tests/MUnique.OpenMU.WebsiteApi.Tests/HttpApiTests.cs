// <copyright file="HttpApiTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Tests;

using System.Net;
using System.Text;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.WebsiteApi.Security;

/// <summary>
/// Tests the HTTP layer (signature, replay, rate limits, JSON errors, endpoint shapes) against a real Kestrel on a loopback
/// port with an in-memory store.
/// </summary>
[TestFixture]
public class HttpApiTests
{
    private FakeStore _store = null!;
    private FakeStatus _status = null!;
    private ApiServer _api = null!;

    /// <summary>Starts a fresh API for every test.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task StartAsync()
    {
        this._store = new FakeStore();
        this._status = new FakeStatus();
        this._api = await ApiServer.StartAsync(this._store, this._status);
    }

    /// <summary>Stops the API.</summary>
    /// <returns>The task.</returns>
    [TearDown]
    public async Task StopAsync() => await this._api.DisposeAsync();

    /// <summary>
    /// Missing headers, a wrong key id or secret, a tampered path, query or body, and a stale timestamp are all refused with the
    /// JSON error format and the right code.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RefusesBadRequests()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var good = RequestSigner.Sign(ApiServer.Secret, "GET", "/api/v1/status", now, []);

        var missing = await this._api.SendRawAsync("GET", "/api/v1/status", [], null, null, null);
        Assert.That((missing.Status, missing.Error), Is.EqualTo((401, "bad_signature")));
        Assert.That(missing.Json.GetProperty("message").GetString(), Is.Not.Empty);

        var wrongKey = await this._api.SendRawAsync("GET", "/api/v1/status", [], "other", now, good);
        Assert.That((wrongKey.Status, wrongKey.Error), Is.EqualTo((401, "bad_signature")));

        var wrongSecret = await this._api.SendRawAsync("GET", "/api/v1/status", [], ApiServer.KeyId, now, RequestSigner.Sign("another-secret-another-secret-another", "GET", "/api/v1/status", now, []));
        Assert.That((wrongSecret.Status, wrongSecret.Error), Is.EqualTo((401, "bad_signature")));

        var otherPath = await this._api.SendRawAsync("GET", "/api/v1/rankings/players", [], ApiServer.KeyId, now, good);
        Assert.That(otherPath.Error, Is.EqualTo("bad_signature"), "the signature covers the path");

        var queryOrder = RequestSigner.Sign(ApiServer.Secret, "GET", "/api/v1/rankings/players?limit=5&x=1", now, []);
        var reordered = await this._api.SendRawAsync("GET", "/api/v1/rankings/players?x=1&limit=5", [], ApiServer.KeyId, now, queryOrder);
        Assert.That(reordered.Error, Is.EqualTo("bad_signature"), "the query is signed exactly as sent");

        var bodyBytes = Encoding.UTF8.GetBytes("{\"loginName\":\"abc\",\"password\":\"password1\"}");
        var bodySignature = RequestSigner.Sign(ApiServer.Secret, "POST", "/api/v1/auth/verify", now, bodyBytes);
        var tampered = await this._api.SendRawAsync("POST", "/api/v1/auth/verify", Encoding.UTF8.GetBytes("{\"loginName\":\"abd\",\"password\":\"password1\"}"), ApiServer.KeyId, now, bodySignature);
        Assert.That(tampered.Error, Is.EqualTo("bad_signature"), "the signature covers the body");

        var stale = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 120).ToString();
        var staleResponse = await this._api.SendRawAsync("GET", "/api/v1/status", [], ApiServer.KeyId, stale, RequestSigner.Sign(ApiServer.Secret, "GET", "/api/v1/status", stale, []));
        Assert.That((staleResponse.Status, staleResponse.Error), Is.EqualTo((401, "stale_timestamp")));

        var future = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 120).ToString();
        var futureResponse = await this._api.SendRawAsync("GET", "/api/v1/status", [], ApiServer.KeyId, future, RequestSigner.Sign(ApiServer.Secret, "GET", "/api/v1/status", future, []));
        Assert.That(futureResponse.Error, Is.EqualTo("stale_timestamp"));
    }

    /// <summary>
    /// A correctly signed request is accepted, with an upper-case signature too, and unknown paths answer with a JSON not_found.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task AcceptsSignedRequestsAndAnswersJson404()
    {
        var ok = await this._api.SendAsync("GET", "/api/v1/status");
        Assert.That(ok.Status, Is.EqualTo(200));

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var upper = await this._api.SendRawAsync("GET", "/api/v1/status", [], ApiServer.KeyId, now, RequestSigner.Sign(ApiServer.Secret, "GET", "/api/v1/status", now, []).ToUpperInvariant());
        Assert.That(upper.Status, Is.EqualTo(200));

        var unknown = await this._api.SendAsync("GET", "/api/v1/nothing");
        Assert.That((unknown.Status, unknown.Error), Is.EqualTo((404, "not_found")));
    }

    /// <summary>
    /// Anything which is not signed gets the same answer, whatever the path (the API does not reveal which paths exist).
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UnsignedRequestsDoNotRevealPaths()
    {
        var a = await this._api.SendRawAsync("GET", "/api/v1/status", [], null, null, null);
        var b = await this._api.SendRawAsync("GET", "/api/v1/nothing", [], null, null, null);
        var c = await this._api.SendRawAsync("GET", "/", [], null, null, null);
        Assert.That(new[] { a.Body, b.Body, c.Body }.Distinct().Count(), Is.EqualTo(1));
        Assert.That(a.Status, Is.EqualTo(401));
    }

    /// <summary>
    /// A changing request cannot be sent twice (replay); repeating a read is allowed.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RefusesReplayedChangesButAllowsRepeatedReads()
    {
        this._store.Add("alice", "password1");
        var body = Encoding.UTF8.GetBytes("{\"loginName\":\"alice\",\"password\":\"password1\"}");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = RequestSigner.Sign(ApiServer.Secret, "POST", "/api/v1/auth/verify", now, body);

        var first = await this._api.SendRawAsync("POST", "/api/v1/auth/verify", body, ApiServer.KeyId, now, signature);
        var second = await this._api.SendRawAsync("POST", "/api/v1/auth/verify", body, ApiServer.KeyId, now, signature);
        Assert.That(first.Status, Is.EqualTo(200));
        Assert.That((second.Status, second.Error), Is.EqualTo((401, "bad_signature")));

        var readSignature = RequestSigner.Sign(ApiServer.Secret, "GET", "/api/v1/status", now, []);
        for (var i = 0; i < 3; i++)
        {
            Assert.That((await this._api.SendRawAsync("GET", "/api/v1/status", [], ApiServer.KeyId, now, readSignature)).Status, Is.EqualTo(200));
        }
    }

    /// <summary>
    /// Too many requests from one client address are answered with 429 rate_limited; another address is not affected.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LimitsRequestsPerClientAddress()
    {
        await this._api.DisposeAsync();
        this._api = await ApiServer.StartAsync(this._store, this._status, o => new WebsiteApiOptions
        {
            EndPoint = o.EndPoint,
            Keys = o.Keys,
            RequestsPerMinutePerIp = 5,
        });

        for (var i = 0; i < 5; i++)
        {
            Assert.That((await this._api.SendAsync("GET", "/api/v1/status", clientIp: "198.51.100.9")).Status, Is.EqualTo(200));
        }

        var limited = await this._api.SendAsync("GET", "/api/v1/status", clientIp: "198.51.100.9");
        Assert.That((limited.Status, limited.Error), Is.EqualTo((429, "rate_limited")));
        Assert.That((await this._api.SendAsync("GET", "/api/v1/status", clientIp: "198.51.100.10")).Status, Is.EqualTo(200));
    }

    /// <summary>
    /// Five wrong passwords for one account lock further attempts for that account (even with the right password), but not other accounts.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LimitsLoginAttemptsPerAccount()
    {
        await this._api.DisposeAsync();
        this._api = await ApiServer.StartAsync(this._store, this._status, o => new WebsiteApiOptions
        {
            EndPoint = o.EndPoint,
            Keys = o.Keys,
            AuthAttemptsPer10MinutesPerAccount = 5,
        });
        this._store.Add("alice", "password1");
        this._store.Add("bob", "password2");

        for (var i = 0; i < 5; i++)
        {
            var wrong = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "alice", password = $"wrong-password-{i}" }, clientIp: $"198.51.100.{i + 20}");
            Assert.That(wrong.Error, Is.EqualTo("invalid_credentials"));
        }

        var locked = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "alice", password = "password1" }, clientIp: "198.51.100.99");
        Assert.That((locked.Status, locked.Error), Is.EqualTo((429, "rate_limited")));

        var other = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "bob", password = "password2" }, clientIp: "198.51.100.98");
        Assert.That(other.Status, Is.EqualTo(200));
    }

    /// <summary>
    /// A client address may register only a few accounts per hour.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LimitsRegistrationsPerClientAddress()
    {
        for (var i = 0; i < 5; i++)
        {
            var response = await this._api.SendAsync("POST", "/api/v1/accounts", new { loginName = $"user{i}", password = "password1", email = $"user{i}@example.com" }, clientIp: "198.51.100.7");
            Assert.That(response.Status, Is.EqualTo(201));
        }

        var limited = await this._api.SendAsync("POST", "/api/v1/accounts", new { loginName = "user9", password = "password1", email = "user9@example.com" }, clientIp: "198.51.100.7");
        Assert.That((limited.Status, limited.Error), Is.EqualTo((429, "rate_limited")));
    }

    /// <summary>
    /// Repeated unsigned requests from one connection end in 429 instead of endless signature checks.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task StopsRepeatedFailures()
    {
        var last = new ApiResponse(0, "{}");
        for (var i = 0; i < 25; i++)
        {
            last = await this._api.SendRawAsync("GET", "/api/v1/status", [], null, null, null);
        }

        Assert.That(last.Status, Is.EqualTo(429));
    }

    /// <summary>
    /// The status, rankings, character and guild endpoints answer in the shape of the contract.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ReadEndpointsHaveTheContractShape()
    {
        this._status.Online.Add("Hero");

        var status = (await this._api.SendAsync("GET", "/api/v1/status")).Json;
        Assert.That(status.GetProperty("online").GetBoolean(), Is.True);
        Assert.That(status.GetProperty("players").GetInt32(), Is.EqualTo(12));
        Assert.That(status.GetProperty("maxPlayers").GetInt32(), Is.EqualTo(400));
        Assert.That(status.GetProperty("uptimeSince").GetDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero)));

        // The single game servers: id, configured name, state and counts.
        var realms = status.GetProperty("realms");
        Assert.That(realms.GetArrayLength(), Is.EqualTo(2));
        Assert.That(realms[0].EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "name", "online", "players", "maxPlayers" }));
        Assert.That(realms[0].GetProperty("name").GetString(), Is.EqualTo("Emberion 50x"));
        Assert.That(realms[0].GetProperty("online").GetBoolean(), Is.True);
        Assert.That(realms[0].GetProperty("players").GetInt32(), Is.EqualTo(9));
        Assert.That(realms[1].GetProperty("online").GetBoolean(), Is.False);
        Assert.That(realms[1].GetProperty("players").GetInt32(), Is.EqualTo(0));

        var players = (await this._api.SendAsync("GET", "/api/v1/rankings/players?limit=50")).Json;
        var first = players[0];
        Assert.That(first.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "rank", "name", "class", "level", "resets", "masterLevel", "guild" }));
        Assert.That(first.GetProperty("name").GetString(), Is.EqualTo("Hero"));

        var guilds = (await this._api.SendAsync("GET", "/api/v1/rankings/guilds")).Json;
        Assert.That(guilds[0].EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "rank", "name", "score", "members", "master" }));

        var character = (await this._api.SendAsync("GET", "/api/v1/characters/Hero")).Json;
        Assert.That(character.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "name", "class", "level", "resets", "masterLevel", "guild", "online", "lastSeen" }));
        Assert.That(character.GetProperty("online").GetBoolean(), Is.True);
        Assert.That(character.GetProperty("lastSeen").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));

        var guild = (await this._api.SendAsync("GET", "/api/v1/guilds/Guild1")).Json;
        Assert.That(guild.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "name", "score", "master", "members", "logoPng" }));
        var png = Convert.FromBase64String(guild.GetProperty("logoPng").GetString()!);
        Assert.That(png.Take(4), Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        Assert.That(guild.GetProperty("members")[0].EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "name", "level" }));

        Assert.That((await this._api.SendAsync("GET", "/api/v1/characters/Nobody")).Error, Is.EqualTo("not_found"));
        Assert.That((await this._api.SendAsync("GET", "/api/v1/characters/waytoolongname")).Status, Is.EqualTo(404));
        Assert.That((await this._api.SendAsync("GET", "/api/v1/guilds/Nope")).Status, Is.EqualTo(404));
    }

    /// <summary>
    /// A new account can be registered and verified; the stored hash is BCrypt, never returned, and the e-mail is not echoed.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RegistersAndVerifiesAnAccount()
    {
        var created = await this._api.SendAsync("POST", "/api/v1/accounts", new { loginName = "Newbie1", password = "secret-pass1", email = "newbie@example.com" });
        Assert.That(created.Status, Is.EqualTo(201));
        Assert.That(created.Json.GetProperty("loginName").GetString(), Is.EqualTo("Newbie1"));
        Assert.That(created.Json.TryGetProperty("id", out var id) && Guid.TryParse(id.GetString(), out _), Is.True);
        Assert.That(created.Body, Does.Not.Contain("newbie@example.com"));

        var stored = this._store.Accounts["Newbie1"];
        Assert.That(stored.Record.PasswordHash, Does.StartWith("$2"), "BCrypt hash");
        Assert.That(BCrypt.Net.BCrypt.Verify("secret-pass1", stored.Record.PasswordHash), Is.True);
        Assert.That(stored.Record.State, Is.EqualTo(AccountState.Normal));

        var verified = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "Newbie1", password = "secret-pass1" });
        Assert.That(verified.Status, Is.EqualTo(200));
        Assert.That(verified.Json.GetProperty("ok").GetBoolean(), Is.True);
        Assert.That(verified.Json.GetProperty("loginName").GetString(), Is.EqualTo("Newbie1"));
        Assert.That(verified.Json.GetProperty("state").GetString(), Is.EqualTo("Normal"));
        Assert.That(verified.Body, Does.Not.Contain("$2").And.Not.Contain("example.com").And.Not.Contain("passwordHash"));
    }

    /// <summary>
    /// Invalid input is answered with 422 and the matching code; used names and e-mail addresses with 409, ignoring the case.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RegistrationValidatesAndChecksUniqueness()
    {
        this._store.Add("Taken", "password1", email: "taken@example.com");

        async Task<ApiResponse> Register(object body, string ip) => await this._api.SendAsync("POST", "/api/v1/accounts", body, clientIp: ip);

        var shortName = await Register(new { loginName = "ab", password = "password1", email = "a@example.com" }, "10.0.0.1");
        Assert.That((shortName.Status, shortName.Error), Is.EqualTo((422, "invalid_name")));
        Assert.That((await Register(new { loginName = "elevenchars", password = "password1", email = "a@example.com" }, "10.0.0.1")).Error, Is.EqualTo("invalid_name"));
        Assert.That((await Register(new { loginName = "Good1", password = "short", email = "a@example.com" }, "10.0.0.1")).Error, Is.EqualTo("invalid_password"));
        Assert.That((await Register(new { loginName = "Good1", password = "this-password-is-far-too-long", email = "a@example.com" }, "10.0.0.1")).Error, Is.EqualTo("invalid_password"));
        Assert.That((await Register(new { loginName = "Good1", password = "password1", email = "not-an-email" }, "10.0.0.1")).Error, Is.EqualTo("invalid_email"));
        Assert.That((await Register(new { loginName = "Good1", password = "password1" }, "10.0.0.2")).Error, Is.EqualTo("invalid_email"));
        Assert.That((await Register(new { loginName = 12345, password = "password1", email = "a@example.com" }, "10.0.0.2")).Error, Is.EqualTo("invalid_name"));

        var nameTaken = await Register(new { loginName = "TAKEN", password = "password1", email = "other@example.com" }, "10.0.0.3");
        Assert.That((nameTaken.Status, nameTaken.Error), Is.EqualTo((409, "name_taken")));
        var emailTaken = await Register(new { loginName = "Fresh1", password = "password1", email = "TAKEN@example.com" }, "10.0.0.3");
        Assert.That((emailTaken.Status, emailTaken.Error), Is.EqualTo((409, "email_taken")));

        Assert.That(this._store.Accounts.Keys, Is.EquivalentTo(new[] { "Taken" }), "nothing was created");
    }

    /// <summary>
    /// Wrong password, unknown name and banned account all get the identical 401 answer; template accounts cannot verify either.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task VerifyAnswersTheSameForEveryFailure()
    {
        this._store.Add("alice", "password1");
        this._store.Add("banned1", "password1", AccountState.Banned);
        this._store.Add("tempban", "password1", AccountState.TemporarilyBanned);
        this._store.Add("template", "password1", template: true);

        var wrong = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "alice", password = "nope-nope-1" });
        var unknown = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "nobody", password = "password1" });
        var banned = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "banned1", password = "password1" });
        var tempBanned = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "tempban", password = "password1" });
        var template = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "template", password = "password1" });
        var broken = await this._api.SendAsync("POST", "/api/v1/auth/verify", new { });

        Assert.That(wrong.Status, Is.EqualTo(401));
        Assert.That(new[] { wrong.Body, unknown.Body, banned.Body, tempBanned.Body, template.Body, broken.Body }.Distinct().Count(), Is.EqualTo(1));
        Assert.That(wrong.Error, Is.EqualTo("invalid_credentials"));
    }

    /// <summary>
    /// The password can be changed with the current password; afterwards only the new one works. A wrong current password changes nothing.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ChangesThePassword()
    {
        this._store.Add("alice", "password1");

        var wrong = await this._api.SendAsync("POST", "/api/v1/accounts/password", new { loginName = "alice", currentPassword = "wrong-pass-1", newPassword = "brand-new-pass1" });
        Assert.That((wrong.Status, wrong.Error), Is.EqualTo((401, "invalid_credentials")));

        var weak = await this._api.SendAsync("POST", "/api/v1/accounts/password", new { loginName = "alice", currentPassword = "password1", newPassword = "short" });
        Assert.That((weak.Status, weak.Error), Is.EqualTo((422, "invalid_password")));

        var changed = await this._api.SendAsync("POST", "/api/v1/accounts/password", new { loginName = "alice", currentPassword = "password1", newPassword = "brand-new-pass1" });
        Assert.That(changed.Status, Is.EqualTo(204));
        Assert.That(changed.Body, Is.Empty);

        Assert.That((await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "alice", password = "password1" })).Status, Is.EqualTo(401));
        Assert.That((await this._api.SendAsync("POST", "/api/v1/auth/verify", new { loginName = "alice", password = "brand-new-pass1" })).Status, Is.EqualTo(200));
    }

    /// <summary>
    /// The character list of an account is returned for usable accounts only; banned, template and unknown names are not found.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ListsAccountCharacters()
    {
        this._store.Add("alice", "password1");
        this._store.Add("banned1", "password1", AccountState.Banned);

        var list = (await this._api.SendAsync("GET", "/api/v1/accounts/alice/characters")).Json;
        Assert.That(list[0].EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "name", "class", "level", "resets", "masterLevel", "stats", "points", "hidden", "online", "pk", "pkState" }));
        Assert.That(list[0].GetProperty("stats").EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "str", "agi", "vit", "ene", "cmd" }));
        Assert.That(list[0].GetProperty("stats").GetProperty("cmd").GetInt32(), Is.EqualTo(0));
        Assert.That(list[0].GetProperty("points").GetInt32(), Is.EqualTo(25));
        Assert.That(list[0].GetProperty("pk").GetInt32(), Is.EqualTo(7));
        Assert.That(list[0].GetProperty("online").GetBoolean(), Is.False);
        this._status.Online.Add("Hero");
        Assert.That((await this._api.SendAsync("GET", "/api/v1/accounts/alice/characters")).Json[0].GetProperty("online").GetBoolean(), Is.True);

        Assert.That((await this._api.SendAsync("GET", "/api/v1/accounts/banned1/characters")).Status, Is.EqualTo(404));
        Assert.That((await this._api.SendAsync("GET", "/api/v1/accounts/nobody/characters")).Status, Is.EqualTo(404));
    }

    /// <summary>
    /// Malformed JSON and oversized bodies do not crash the API.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HandlesMalformedAndOversizedBodies()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var garbage = Encoding.UTF8.GetBytes("{not json");
        var response = await this._api.SendRawAsync("POST", "/api/v1/auth/verify", garbage, ApiServer.KeyId, now, RequestSigner.Sign(ApiServer.Secret, "POST", "/api/v1/auth/verify", now, garbage));
        Assert.That((response.Status, response.Error), Is.EqualTo((401, "invalid_credentials")));

        var huge = new byte[WebsiteApiHost.MaximumBodySize + 100];
        Array.Fill(huge, (byte)'a');
        var tooBig = await this._api.SendRawAsync("POST", "/api/v1/accounts", huge, ApiServer.KeyId, now, RequestSigner.Sign(ApiServer.Secret, "POST", "/api/v1/accounts", now, huge));
        Assert.That((tooBig.Status, tooBig.Error), Is.EqualTo(((int)HttpStatusCode.RequestEntityTooLarge, "bad_request")));
    }
}
