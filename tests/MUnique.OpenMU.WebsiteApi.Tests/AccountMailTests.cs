// <copyright file="AccountMailTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.WebsiteApi.Accounts;
using MUnique.OpenMU.WebsiteApi.Data;
using MUnique.OpenMU.WebsiteApi.Security;

/// <summary>
/// Tests the account page, e-mail change, e-mail verification and password reset endpoints (HTTP layer with an in-memory store)
/// and the token rules of the service (lifetime, single use, replacement) with a controlled clock.
/// </summary>
[TestFixture]
public class AccountMailTests
{
    private FakeStore _store = null!;
    private ApiServer _api = null!;
    private int _requestCounter;
    private long _lastTimestamp = long.MaxValue;

    /// <summary>Starts a fresh API for every test.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task StartAsync()
    {
        this._store = new FakeStore();
        this._api = await ApiServer.StartAsync(this._store);
        this._requestCounter = 0;
        this._lastTimestamp = long.MaxValue;
    }

    /// <summary>Stops the API.</summary>
    /// <returns>The task.</returns>
    [TearDown]
    public async Task StopAsync() => await this._api.DisposeAsync();

    /// <summary>
    /// The account page data has the contract shape; unknown, banned, template and bot accounts are not found.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ShowsTheAccount()
    {
        this._store.Add("alice", "password1", email: "alice@example.com");
        this._store.Add("banned1", "password1", AccountState.Banned);
        this._store.Add("templ1", "password1", template: true);

        var response = await this._api.SendAsync("GET", "/api/v1/accounts/alice");
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Json.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "loginName", "email", "emailVerified", "registeredAt" }));
        Assert.That(response.Json.GetProperty("email").GetString(), Is.EqualTo("alice@example.com"));
        Assert.That(response.Json.GetProperty("emailVerified").GetBoolean(), Is.False);
        Assert.That(response.Json.GetProperty("registeredAt").GetDateTime().Kind, Is.EqualTo(DateTimeKind.Utc));

        foreach (var name in new[] { "banned1", "templ1", "nobody", "x" })
        {
            Assert.That((await this._api.SendAsync("GET", "/api/v1/accounts/" + name)).Status, Is.EqualTo(404), name);
        }
    }

    /// <summary>
    /// Changing the e-mail address needs the password, checks the address, and un-verifies it and voids the open tokens.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ChangesTheEmail()
    {
        var alice = this._store.Add("alice", "password1", email: "alice@example.com");
        this._store.Add("bob", "password1", email: "bob@example.com");
        this._store.Verified.Add(alice.Id);
        this._store.Tokens.Add((alice.Id, TokenPurpose.ResetPassword, "hash", DateTimeOffset.UtcNow.AddHours(1)));

        var wrongPassword = await this.PostAsync("/api/v1/accounts/email", new { loginName = "alice", password = "wrongpass1", newEmail = "new@example.com" });
        Assert.That((wrongPassword.Status, wrongPassword.Error), Is.EqualTo((401, "invalid_credentials")));

        var invalid = await this.PostAsync("/api/v1/accounts/email", new { loginName = "alice", password = "password1", newEmail = "not-an-email" });
        Assert.That((invalid.Status, invalid.Error), Is.EqualTo((422, "invalid_email")));

        var taken = await this.PostAsync("/api/v1/accounts/email", new { loginName = "alice", password = "password1", newEmail = "BOB@example.com" });
        Assert.That((taken.Status, taken.Error), Is.EqualTo((409, "email_taken")));
        Assert.That(this._store.Verified, Does.Contain(alice.Id), "a refused change changes nothing");

        var changed = await this.PostAsync("/api/v1/accounts/email", new { loginName = "alice", password = "password1", newEmail = "new@example.com" });
        Assert.That(changed.Status, Is.EqualTo(204));
        Assert.That(this._store.Verified, Does.Not.Contain(alice.Id));
        Assert.That(this._store.Tokens, Is.Empty);

        var page = (await this._api.SendAsync("GET", "/api/v1/accounts/alice")).Json;
        Assert.That(page.GetProperty("email").GetString(), Is.EqualTo("new@example.com"));
        Assert.That(page.GetProperty("emailVerified").GetBoolean(), Is.False);
    }

    /// <summary>
    /// The full verification flow: request a token, confirm it once, and see the flag on the account page.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task VerifiesTheEmail()
    {
        var alice = this._store.Add("alice", "password1", email: "alice@example.com");

        var request = await this.PostAsync("/api/v1/accounts/verify-email/request", new { loginName = "alice" });
        Assert.That(request.Status, Is.EqualTo(200));
        Assert.That(request.Json.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "email", "token" }));
        Assert.That(request.Json.GetProperty("email").GetString(), Is.EqualTo("alice@example.com"));
        var token = request.Json.GetProperty("token").GetString()!;
        Assert.That(token, Does.Match("^[0-9a-f]{64}$"));
        Assert.That(this._store.Tokens.Single().Hash, Is.Not.EqualTo(token), "only a hash is stored");
        Assert.That(this._store.Tokens.Single().Hash, Is.EqualTo(Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)))));

        var confirm = await this.PostAsync("/api/v1/accounts/verify-email/confirm", new { token });
        Assert.That(confirm.Status, Is.EqualTo(200));
        Assert.That(confirm.Json.GetProperty("loginName").GetString(), Is.EqualTo("alice"));
        Assert.That(this._store.Verified, Does.Contain(alice.Id));
        Assert.That((await this._api.SendAsync("GET", "/api/v1/accounts/alice")).Json.GetProperty("emailVerified").GetBoolean(), Is.True);

        var again = await this.PostAsync("/api/v1/accounts/verify-email/confirm", new { token, again = 1 });
        Assert.That((again.Status, again.Error), Is.EqualTo((422, "invalid_token")));

        var already = await this.PostAsync("/api/v1/accounts/verify-email/request", new { loginName = "alice", again = 1 });
        Assert.That((already.Status, already.Error), Is.EqualTo((409, "already_verified")));
    }

    /// <summary>
    /// Verification requests for unknown, banned or address-less accounts, and bad tokens, get the documented errors.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task VerificationRefusals()
    {
        this._store.Add("banned1", "password1", AccountState.Banned);
        this._store.Add("noemail", "password1", email: string.Empty);

        Assert.That((await this.PostAsync("/api/v1/accounts/verify-email/request", new { loginName = "nobody" })).Status, Is.EqualTo(404));
        Assert.That((await this.PostAsync("/api/v1/accounts/verify-email/request", new { loginName = "banned1" })).Status, Is.EqualTo(404));
        var noEmail = await this.PostAsync("/api/v1/accounts/verify-email/request", new { loginName = "noemail" });
        Assert.That((noEmail.Status, noEmail.Error), Is.EqualTo((422, "invalid_email")));

        foreach (var token in new[] { string.Empty, "abc", new string('g', 64), new string('a', 64), new string('A', 64) })
        {
            var bad = await this.PostAsync("/api/v1/accounts/verify-email/confirm", new { token });
            Assert.That((bad.Status, bad.Error), Is.EqualTo((422, "invalid_token")), token);
        }

        var missing = await this.PostAsync("/api/v1/accounts/verify-email/confirm", new { });
        Assert.That(missing.Error, Is.EqualTo("invalid_token"));
    }

    /// <summary>
    /// At most 3 verification tokens per hour per account; every request is counted, also for unknown names.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LimitsVerificationRequestsPerAccount()
    {
        this._store.Add("alice", "password1", email: "alice@example.com");
        for (var i = 0; i < 3; i++)
        {
            Assert.That((await this.PostAsync("/api/v1/accounts/verify-email/request", new { loginName = "alice" })).Status, Is.EqualTo(200));
        }

        var limited = await this.PostAsync("/api/v1/accounts/verify-email/request", new { loginName = "alice" });
        Assert.That((limited.Status, limited.Error), Is.EqualTo((429, "rate_limited")));
        Assert.That(this._store.Tokens, Has.Count.EqualTo(1), "one active token per account");
    }

    /// <summary>
    /// Reset requests find only normal accounts; everything else (and unknown addresses) is <c>found: false</c>.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ResetRequestFindsOnlyNormalAccounts()
    {
        this._store.Add("alice", "password1", email: "alice@example.com");
        this._store.Add("banned1", "password1", AccountState.Banned, "banned@example.com");
        this._store.Add("gm1", "password1", AccountState.GameMaster, "gm@example.com");
        this._store.Add("templ1", "password1", email: "templ@example.com", template: true);
        this._store.Add("twin1", "password1", email: "twin@example.com");
        this._store.Add("twin2", "password1", email: "TWIN@example.com");

        foreach (var email in new[] { "banned@example.com", "gm@example.com", "templ@example.com", "twin@example.com", "nobody@example.com", "not-an-email" })
        {
            var response = await this.PostAsync("/api/v1/accounts/reset/request", new { email }, clientIp: "198.51.100." + this._requestCounter);
            Assert.That(response.Status, Is.EqualTo(200), email);
            Assert.That(response.Body, Is.EqualTo("{\"found\":false}"), email);
        }

        var found = await this.PostAsync("/api/v1/accounts/reset/request", new { email = "ALICE@example.com" }, clientIp: "198.51.100.200");
        Assert.That(found.Status, Is.EqualTo(200));
        Assert.That(found.Json.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "found", "loginName", "email", "token" }));
        Assert.That(found.Json.GetProperty("found").GetBoolean(), Is.True);
        Assert.That(found.Json.GetProperty("loginName").GetString(), Is.EqualTo("alice"));
        Assert.That(found.Json.GetProperty("email").GetString(), Is.EqualTo("alice@example.com"), "the stored address");
        Assert.That(found.Json.GetProperty("token").GetString(), Does.Match("^[0-9a-f]{64}$"));
    }

    /// <summary>
    /// The reset flow: a wrong new password keeps the token, a good one changes the password, uses the token up and voids the others.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ResetsThePassword()
    {
        var alice = this._store.Add("alice", "password1", email: "alice@example.com");
        var token = (await this.PostAsync("/api/v1/accounts/reset/request", new { email = "alice@example.com" })).Json.GetProperty("token").GetString()!;
        this._store.Tokens.Add((alice.Id, TokenPurpose.VerifyEmail, "other-token-hash", DateTimeOffset.UtcNow.AddHours(1)));

        var weak = await this.PostAsync("/api/v1/accounts/reset/confirm", new { token, newPassword = "short" });
        Assert.That((weak.Status, weak.Error), Is.EqualTo((422, "invalid_password")));

        var changed = await this.PostAsync("/api/v1/accounts/reset/confirm", new { token, newPassword = "brandnewpass1" });
        Assert.That(changed.Status, Is.EqualTo(204), "the token survived the invalid password");
        Assert.That(this._store.Tokens, Is.Empty, "all tokens of the account are void");

        Assert.That((await this.PostAsync("/api/v1/auth/verify", new { loginName = "alice", password = "brandnewpass1" })).Status, Is.EqualTo(200));
        Assert.That((await this.PostAsync("/api/v1/auth/verify", new { loginName = "alice", password = "password1" })).Status, Is.EqualTo(401));

        var again = await this.PostAsync("/api/v1/accounts/reset/confirm", new { token, newPassword = "anotherpass12" });
        Assert.That((again.Status, again.Error), Is.EqualTo((422, "invalid_token")));

        var unknown = await this.PostAsync("/api/v1/accounts/reset/confirm", new { token = new string('a', 64), newPassword = "anotherpass12" });
        Assert.That((unknown.Status, unknown.Error), Is.EqualTo((422, "invalid_token")));
    }

    /// <summary>
    /// At most 5 reset requests per hour per client address and 3 per e-mail address; unknown addresses count, too.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task LimitsResetRequests()
    {
        this._store.Add("alice", "password1", email: "alice@example.com");

        for (var i = 0; i < 5; i++)
        {
            Assert.That((await this.PostAsync("/api/v1/accounts/reset/request", new { email = $"nobody{i}@example.com" }, clientIp: "203.0.113.5")).Status, Is.EqualTo(200));
        }

        var byClient = await this.PostAsync("/api/v1/accounts/reset/request", new { email = "nobody9@example.com" }, clientIp: "203.0.113.5");
        Assert.That((byClient.Status, byClient.Error), Is.EqualTo((429, "rate_limited")));

        for (var i = 0; i < 3; i++)
        {
            Assert.That((await this.PostAsync("/api/v1/accounts/reset/request", new { email = "alice@example.com" }, clientIp: "203.0.113." + (10 + i))).Status, Is.EqualTo(200));
        }

        var byMail = await this.PostAsync("/api/v1/accounts/reset/request", new { email = "alice@example.com" }, clientIp: "203.0.113.99");
        Assert.That((byMail.Status, byMail.Error), Is.EqualTo((429, "rate_limited")));
    }

    /// <summary>
    /// Tokens work for their lifetime only (24 h for the e-mail, 1 h for the reset), once, and a new token replaces the old one.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TokensExpireAreSingleUseAndReplaceEachOther()
    {
        var now = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        var service = new WebsiteAccountService(this._store, () => now);
        this._store.Add("alice", "password1", email: "alice@example.com");

        var first = (await service.RequestEmailVerificationAsync("alice", default)).Token;
        var second = (await service.RequestEmailVerificationAsync("alice", default)).Token;
        Assert.That(first, Is.Not.EqualTo(second));
        Assert.That(await service.ConfirmEmailVerificationAsync(first, default), Is.Null, "the newer token replaced the older");

        now = now.AddHours(23).AddMinutes(59);
        Assert.That(await service.ConfirmEmailVerificationAsync(second, default), Is.EqualTo("alice"));

        // Expired: a reset token after one hour.
        now = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        var reset = (await service.RequestPasswordResetAsync("alice@example.com", default))!.Value.Token;
        now = now.AddMinutes(61);
        Assert.That(await service.ConfirmPasswordResetAsync(reset, "brandnewpass1", default), Is.EqualTo(PasswordResetOutcome.InvalidToken));

        // Expired: a verification token after 24 hours (use a second account, the first one is verified).
        this._store.Add("carol", "password1", email: "carol@example.com");
        var late = (await service.RequestEmailVerificationAsync("carol", default)).Token;
        now = now.AddHours(24).AddSeconds(1);
        Assert.That(await service.ConfirmEmailVerificationAsync(late, default), Is.Null);
    }

    private async Task<ApiResponse> PostAsync(string path, object body, string? clientIp = null)
    {
        // Every request gets another timestamp: an identical signature within the window counts as a replay.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body);
        // Strictly decreasing, so two requests never share a timestamp even when the clock ticks between them.
        this._lastTimestamp = Math.Min(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), this._lastTimestamp - 1);
        this._requestCounter++;
        var timestamp = this._lastTimestamp.ToString();
        return await this._api.SendRawAsync("POST", path, bytes, ApiServer.KeyId, timestamp, RequestSigner.Sign(ApiServer.Secret, "POST", path, timestamp, bytes), clientIp);
    }
}
