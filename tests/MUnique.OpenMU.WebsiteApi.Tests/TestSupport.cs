// <copyright file="TestSupport.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Tests;

using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.WebsiteApi.Data;
using MUnique.OpenMU.WebsiteApi.Security;

/// <summary>
/// A running website API on a free loopback port, plus a client which signs its requests.
/// </summary>
public sealed class ApiServer : IAsyncDisposable
{
    /// <summary>The secret of the test key.</summary>
    public const string Secret = "test-secret-test-secret-test-secret-123";

    /// <summary>The id of the test key.</summary>
    public const string KeyId = "site";

    private readonly WebApplication _app;
    private readonly HttpClient _http;

    private ApiServer(WebApplication app, HttpClient http, int port)
    {
        this._app = app;
        this._http = http;
        this.Port = port;
    }

    /// <summary>Gets the port.</summary>
    public int Port { get; }

    /// <summary>
    /// Starts the API.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="status">The status source.</param>
    /// <param name="configure">Changes the options.</param>
    /// <returns>The running API.</returns>
    public static async Task<ApiServer> StartAsync(IWebsiteStore store, IWebsiteStatusSource? status = null, Func<WebsiteApiOptions, WebsiteApiOptions>? configure = null)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var options = new WebsiteApiOptions
        {
            EndPoint = new IPEndPoint(IPAddress.Loopback, port),
            Keys = new Dictionary<string, string> { [KeyId] = Secret },
        };
        options = configure?.Invoke(options) ?? options;
        var app = WebsiteApiHost.Build(options, store, status ?? new FakeStatus(), new TestLoggerFactory());
        await app.StartAsync().ConfigureAwait(false);
        return new ApiServer(app, new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") }, port);
    }

    /// <summary>
    /// Sends a correctly signed request.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="pathAndQuery">The path and query.</param>
    /// <param name="body">The JSON body or <c>null</c>.</param>
    /// <param name="clientIp">The value of the X-Client-Ip header or <c>null</c>.</param>
    /// <returns>The response.</returns>
    public Task<ApiResponse> SendAsync(string method, string pathAndQuery, object? body = null, string? clientIp = null)
    {
        var bytes = body is null ? [] : JsonSerializer.SerializeToUtf8Bytes(body);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        return this.SendRawAsync(method, pathAndQuery, bytes, KeyId, timestamp, RequestSigner.Sign(Secret, method, pathAndQuery, timestamp, bytes), clientIp);
    }

    /// <summary>
    /// Sends a request with explicit signature headers (to test the refusals).
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="pathAndQuery">The path and query.</param>
    /// <param name="body">The body bytes.</param>
    /// <param name="keyId">The key id, or <c>null</c> to leave the header out.</param>
    /// <param name="timestamp">The timestamp, or <c>null</c> to leave the header out.</param>
    /// <param name="signature">The signature, or <c>null</c> to leave the header out.</param>
    /// <param name="clientIp">The value of the X-Client-Ip header or <c>null</c>.</param>
    /// <returns>The response.</returns>
    public async Task<ApiResponse> SendRawAsync(string method, string pathAndQuery, byte[] body, string? keyId, string? timestamp, string? signature, string? clientIp = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), pathAndQuery);
        if (body.Length > 0)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new("application/json");
        }

        if (keyId is not null)
        {
            request.Headers.Add("X-Key-Id", keyId);
        }

        if (timestamp is not null)
        {
            request.Headers.Add("X-Timestamp", timestamp);
        }

        if (signature is not null)
        {
            request.Headers.Add("X-Signature", signature);
        }

        if (clientIp is not null)
        {
            request.Headers.Add("X-Client-Ip", clientIp);
        }

        using var response = await this._http.SendAsync(request).ConfigureAwait(false);
        return new ApiResponse((int)response.StatusCode, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        this._http.Dispose();
        await this._app.StopAsync().ConfigureAwait(false);
        await this._app.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// A response of the API.
/// </summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Body">The body text.</param>
public sealed record ApiResponse(int Status, string Body)
{
    /// <summary>Gets the parsed JSON body.</summary>
    public JsonElement Json => JsonDocument.Parse(this.Body).RootElement;

    /// <summary>Gets the <c>error</c> code of an error response.</summary>
    public string? Error => this.Json.TryGetProperty("error", out var error) ? error.GetString() : null;
}

/// <summary>
/// A fixed server status.
/// </summary>
public sealed class FakeStatus : IWebsiteStatusSource
{
    /// <summary>Gets or sets the names of the characters which are online.</summary>
    public HashSet<string> Online { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<string>> GetOnlineCharacterNamesAsync() => new(this.Online.ToList());

    /// <inheritdoc />
    public ServerStatus GetStatus() => new(
        true,
        12,
        400,
        new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero),
        [new RealmStatus(0, "Emberion 50x", true, 9, 200), new RealmStatus(1, "Emberion 5x", false, 0, 200)]);

    /// <inheritdoc />
    public bool IsCharacterOnline(string characterName) => this.Online.Contains(characterName);
}

/// <summary>
/// An in-memory store.
/// </summary>
public sealed class FakeStore : IWebsiteStore
{
    /// <summary>Gets the accounts by login name.</summary>
    public Dictionary<string, (AccountRecord Record, string Email)> Accounts { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the number of reads of the account list, to see what the endpoints touch.</summary>
    public int Reads { get; private set; }

    /// <summary>Adds an account with a BCrypt hash of the password.</summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="password">The password.</param>
    /// <param name="state">The state.</param>
    /// <param name="email">The e-mail address.</param>
    /// <param name="template">Whether it is a template account.</param>
    /// <returns>The record.</returns>
    public AccountRecord Add(string loginName, string password, AccountState state = AccountState.Normal, string email = "x@example.com", bool template = false)
    {
        var record = new AccountRecord(Guid.NewGuid(), loginName, BCrypt.Net.BCrypt.HashPassword(password, 4), state, template, false);
        this.Accounts[loginName] = (record, email);
        return record;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PlayerRankEntry>> GetPlayerRankingAsync(int limit, CancellationToken cancellationToken)
    {
        this.Reads++;
        return Task.FromResult<IReadOnlyList<PlayerRankEntry>>(new[] { new PlayerRankEntry(1, "Hero", "Blade Master", 400, 3, 120, "Guild1") }.Take(limit).ToList());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GuildRankEntry>> GetGuildRankingAsync(int limit, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<GuildRankEntry>>(new[] { new GuildRankEntry(1, "Guild1", 500, 4, "Hero") }.Take(limit).ToList());

    /// <inheritdoc />
    public Task<CharacterProfile?> GetCharacterAsync(string name, CancellationToken cancellationToken)
        => Task.FromResult<CharacterProfile?>(name.Equals("Hero", StringComparison.OrdinalIgnoreCase) ? new CharacterProfile("Hero", "Blade Master", 400, 3, 120, "Guild1") : null);

    /// <inheritdoc />
    public Task<GuildProfile?> GetGuildAsync(string name, CancellationToken cancellationToken)
    {
        var logo = new byte[32];
        logo[0] = 0x44;
        return Task.FromResult<GuildProfile?>(name == "Guild1" ? new GuildProfile("Guild1", 500, "Hero", new[] { new GuildMemberEntry("Hero", 400) }, logo) : null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AccountCharacter>> GetAccountCharactersAsync(Guid accountId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AccountCharacter>>(new[] { new AccountCharacter("Hero", "Blade Master", 400, 3, 120, new CharacterStats(1000, 800, 600, 400, 0), 25, 7, "PlayerKillWarning") });

    /// <inheritdoc />
    public Task<AccountRecord?> FindAccountAsync(string loginName, CancellationToken cancellationToken)
        => Task.FromResult(this.Accounts.TryGetValue(loginName, out var entry) ? entry.Record : null);

    /// <inheritdoc />
    public Task<bool> LoginNameExistsAsync(string loginName, CancellationToken cancellationToken)
        => Task.FromResult(this.Accounts.Keys.Any(k => k.Equals(loginName, StringComparison.OrdinalIgnoreCase)));

    /// <inheritdoc />
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
        => Task.FromResult(this.Accounts.Values.Any(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    /// <inheritdoc />
    public Task<(CreateAccountOutcome Outcome, Guid Id)> CreateAccountAsync(string loginName, string passwordHash, string email, CancellationToken cancellationToken)
    {
        var record = new AccountRecord(Guid.NewGuid(), loginName, passwordHash, AccountState.Normal, false, false);
        this.Accounts[loginName] = (record, email);
        return Task.FromResult((CreateAccountOutcome.Created, record.Id));
    }

    /// <inheritdoc />
    public Task<bool> UpdatePasswordHashAsync(Guid accountId, string passwordHash, CancellationToken cancellationToken)
    {
        var entry = this.Accounts.Values.First(a => a.Record.Id == accountId);
        this.Accounts[entry.Record.LoginName] = (entry.Record with { PasswordHash = passwordHash }, entry.Email);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ValueRankEntry>> GetKillersRankingAsync(int limit, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ValueRankEntry>>(new[] { new ValueRankEntry(1, "Hero", "Blade Master", 7, "Guild1") }.Take(limit).ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<OnlineCharacter>> GetCharactersByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<OnlineCharacter>>(names.Contains("Hero") ? new[] { new OnlineCharacter("Hero", "Blade Master", 400, "Guild1") } : []);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetBannedCharacterNamesAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>(["Cheater"]);

    /// <inheritdoc />
    public Task<SearchResult> SearchAsync(string text, CancellationToken cancellationToken)
        => Task.FromResult(new SearchResult(text.Contains("er", StringComparison.OrdinalIgnoreCase) ? ["Hero"] : [], text.Contains("Gu", StringComparison.OrdinalIgnoreCase) ? ["Guild1"] : []));

    /// <inheritdoc />
    public Task<CastleSiegeInfo> GetCastleSiegeAsync(CancellationToken cancellationToken)
        => Task.FromResult(new CastleSiegeInfo(new SiegeGuild("Guild1", "Hero"), [new SiegeGuild("Guild2", null)]));

    /// <summary>Gets the ids of the accounts with a verified e-mail address.</summary>
    public HashSet<Guid> Verified { get; } = new();

    /// <summary>Gets the stored tokens (hashes only).</summary>
    public List<(Guid AccountId, TokenPurpose Purpose, string Hash, DateTimeOffset Expires)> Tokens { get; } = new();

    /// <inheritdoc />
    public Task<AccountRecord?> FindAccountByIdAsync(Guid accountId, CancellationToken cancellationToken)
        => Task.FromResult<AccountRecord?>(this.Accounts.Values.Select(a => a.Record).FirstOrDefault(r => r.Id == accountId));

    /// <inheritdoc />
    public Task<IReadOnlyList<AccountRecord>> FindAccountsByEmailAsync(string email, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AccountRecord>>(this.Accounts.Values.Where(a => a.Email.Length > 0 && a.Email.Equals(email, StringComparison.OrdinalIgnoreCase)).Select(a => a.Record).ToList());

    /// <inheritdoc />
    public Task<AccountDetails?> GetAccountDetailsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var entry = this.Accounts.Values.FirstOrDefault(a => a.Record.Id == accountId);
        return Task.FromResult<AccountDetails?>(entry.Record is null ? null : new AccountDetails(entry.Email, this.Verified.Contains(accountId), new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc)));
    }

    /// <inheritdoc />
    public Task UpdateEmailAsync(Guid accountId, string email, CancellationToken cancellationToken)
    {
        var entry = this.Accounts.Values.First(a => a.Record.Id == accountId);
        this.Accounts[entry.Record.LoginName] = (entry.Record, email);
        this.Verified.Remove(accountId);
        this.Tokens.RemoveAll(t => t.AccountId == accountId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetEmailVerifiedAsync(Guid accountId, CancellationToken cancellationToken)
    {
        this.Verified.Add(accountId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetTokenAsync(Guid accountId, TokenPurpose purpose, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        this.Tokens.RemoveAll(t => t.AccountId == accountId && t.Purpose == purpose);
        this.Tokens.Add((accountId, purpose, tokenHash, expiresAt));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Guid?> FindTokenAccountAsync(TokenPurpose purpose, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var token = this.Tokens.FirstOrDefault(t => t.Purpose == purpose && t.Hash == tokenHash && t.Expires > now);
        return Task.FromResult<Guid?>(token.Hash is null ? null : token.AccountId);
    }

    /// <inheritdoc />
    public async Task<Guid?> ConsumeTokenAsync(TokenPurpose purpose, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var id = await this.FindTokenAccountAsync(purpose, tokenHash, now, cancellationToken);
        this.Tokens.RemoveAll(t => t.Purpose == purpose && t.Hash == tokenHash);
        return id;
    }

    /// <inheritdoc />
    public Task VoidTokensAsync(Guid accountId, CancellationToken cancellationToken)
    {
        this.Tokens.RemoveAll(t => t.AccountId == accountId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Writes errors of the API under test to the NUnit output, so a 500 can be understood.
/// </summary>
public sealed class TestLoggerFactory : ILoggerFactory
{
    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new TestLogger();

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class TestLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (this.IsEnabled(logLevel))
            {
                TestContext.Progress.WriteLine($"[API {logLevel}] {formatter(state, exception)} {exception}");
            }
        }
    }
}
