// <copyright file="WebsiteApiEndpoints.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MUnique.OpenMU.WebsiteApi.Accounts;
using MUnique.OpenMU.WebsiteApi.Data;
using MUnique.OpenMU.WebsiteApi.Security;

/// <summary>
/// The endpoints of the website API, version 1 (see docs/website-api.md).
/// </summary>
public static class WebsiteApiEndpoints
{
    private const int DefaultRankingLimit = 10;
    private const int MaximumRankingLimit = 100;
    private const int MaximumNameLength = 10;
    private const int MaximumOnlineList = 200;
    private const int MailTokensPerHour = 3;
    private const int ResetRequestsPerHourPerClient = 5;

    /// <summary>
    /// Maps the endpoints below <c>/api/v1</c>.
    /// </summary>
    /// <param name="app">The application.</param>
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/status", GetStatus);
        api.MapGet("/rankings/players", GetPlayerRanking);
        api.MapGet("/rankings/guilds", GetGuildRanking);
        api.MapGet("/rankings/killers", GetKillersRankingAsync);
        api.MapGet("/rankings/{kind}", GetUnsupportedRanking);
        api.MapGet("/online", GetOnlineAsync);
        api.MapGet("/bans", GetBansAsync);
        api.MapGet("/search", SearchAsync);
        api.MapGet("/castle-siege", GetCastleSiegeAsync);
        api.MapGet("/events", GetEvents);
        api.MapGet("/characters/{name}", GetCharacterAsync);
        api.MapGet("/guilds/{name}", GetGuildAsync);
        api.MapPost("/accounts", RegisterAsync);
        api.MapPost("/auth/verify", VerifyAsync);
        api.MapPost("/accounts/password", ChangePasswordAsync);
        api.MapGet("/accounts/{loginName}/characters", GetAccountCharactersAsync);
        api.MapGet("/accounts/{loginName}", GetAccountAsync);
        api.MapPost("/accounts/email", ChangeEmailAsync);
        api.MapPost("/accounts/verify-email/request", RequestEmailVerificationAsync);
        api.MapPost("/accounts/verify-email/confirm", ConfirmEmailVerificationAsync);
        api.MapPost("/accounts/reset/request", RequestPasswordResetAsync);
        api.MapPost("/accounts/reset/confirm", ConfirmPasswordResetAsync);
    }

    private static IResult GetStatus(IWebsiteStatusSource status)
    {
        var s = status.GetStatus();
        return Results.Ok(new
        {
            online = s.Online,
            players = s.Players,
            maxPlayers = s.MaxPlayers,
            uptimeSince = s.UptimeSince,
            realms = s.Realms.Select(r => new { id = r.Id, name = r.Name, online = r.Online, players = r.Players, maxPlayers = r.MaxPlayers }),
        });
    }

    private static async Task<IResult> GetPlayerRanking(HttpContext context, IWebsiteStore store)
    {
        return Results.Ok(await store.GetPlayerRankingAsync(ParseLimit(context), context.RequestAborted).ConfigureAwait(false));
    }

    private static async Task<IResult> GetGuildRanking(HttpContext context, IWebsiteStore store)
    {
        return Results.Ok(await store.GetGuildRankingAsync(ParseLimit(context), context.RequestAborted).ConfigureAwait(false));
    }

    private static async Task<IResult> GetCharacterAsync(string name, HttpContext context, IWebsiteStore store, IWebsiteStatusSource status)
    {
        if (name.Length is 0 or > MaximumNameLength)
        {
            return NotFound();
        }

        var profile = await store.GetCharacterAsync(name, context.RequestAborted).ConfigureAwait(false);
        if (profile is null)
        {
            return NotFound();
        }

        // The database does not store when a character was last online, so lastSeen stays empty.
        return Results.Ok(new
        {
            name = profile.Name,
            @class = profile.Class,
            level = profile.Level,
            resets = profile.Resets,
            masterLevel = profile.MasterLevel,
            guild = profile.Guild,
            online = status.IsCharacterOnline(profile.Name),
            lastSeen = (DateTimeOffset?)null,
        });
    }

    private static async Task<IResult> GetGuildAsync(string name, HttpContext context, IWebsiteStore store)
    {
        if (name.Length is 0 or > 8)
        {
            return NotFound();
        }

        var profile = await store.GetGuildAsync(name, context.RequestAborted).ConfigureAwait(false);
        if (profile is null)
        {
            return NotFound();
        }

        var png = GuildLogoPng.Create(profile.Logo);
        return Results.Ok(new
        {
            name = profile.Name,
            score = profile.Score,
            master = profile.Master,
            members = profile.Members,
            logoPng = png is null ? null : Convert.ToBase64String(png),
        });
    }

    private static async Task<IResult> RegisterAsync(HttpContext context, WebsiteAccountService accounts, RateLimiter limiter, WebsiteApiOptions options)
    {
        if (!limiter.TryHit("register:" + WebsiteApiHost.GetClientAddress(context), options.RegistrationsPerHourPerIp, TimeSpan.FromHours(1)))
        {
            return Error(StatusCodes.Status429TooManyRequests, "rate_limited", "Too many registrations from this address.");
        }

        var request = await ReadJsonAsync<RegisterRequest>(context).ConfigureAwait(false);
        var (outcome, id) = await accounts.RegisterAsync(request?.LoginName, request?.Password, request?.Email, context.RequestAborted).ConfigureAwait(false);
        return outcome switch
        {
            RegistrationOutcome.Created => Results.Json(new { id, loginName = request!.LoginName }, statusCode: StatusCodes.Status201Created),
            RegistrationOutcome.InvalidName => Error(StatusCodes.Status422UnprocessableEntity, "invalid_name", $"The name needs {AccountRules.MinimumLoginNameLength} to {AccountRules.MaximumLoginNameLength} letters or digits."),
            RegistrationOutcome.InvalidPassword => Error(StatusCodes.Status422UnprocessableEntity, "invalid_password", $"The password needs {AccountRules.MinimumPasswordLength} to {AccountRules.MaximumPasswordLength} printable characters without spaces."),
            RegistrationOutcome.InvalidEmail => Error(StatusCodes.Status422UnprocessableEntity, "invalid_email", "The e-mail address is not valid."),
            RegistrationOutcome.NameTaken => Error(StatusCodes.Status409Conflict, "name_taken", "This name is already used."),
            _ => Error(StatusCodes.Status409Conflict, "email_taken", "This e-mail address is already used."),
        };
    }

    private static async Task<IResult> VerifyAsync(HttpContext context, WebsiteAccountService accounts, RateLimiter limiter, WebsiteApiOptions options)
    {
        var request = await ReadJsonAsync<VerifyRequest>(context).ConfigureAwait(false);
        var limited = CheckAccountAttemptLimit(request?.LoginName, context, limiter, options, out var accountKey, out var clientKey);
        if (limited is not null)
        {
            return limited;
        }

        var account = await accounts.VerifyAsync(request?.LoginName, request?.Password, context.RequestAborted).ConfigureAwait(false);
        if (account is null)
        {
            return InvalidCredentials();
        }

        limiter.Reset(accountKey);
        limiter.Reset(clientKey);
        return Results.Ok(new { ok = true, accountId = account.Id, loginName = account.LoginName, state = account.State.ToString() });
    }

    private static async Task<IResult> ChangePasswordAsync(HttpContext context, WebsiteAccountService accounts, RateLimiter limiter, WebsiteApiOptions options)
    {
        var request = await ReadJsonAsync<PasswordRequest>(context).ConfigureAwait(false);
        var limited = CheckAccountAttemptLimit(request?.LoginName, context, limiter, options, out var accountKey, out var clientKey);
        if (limited is not null)
        {
            return limited;
        }

        var outcome = await accounts.ChangePasswordAsync(request?.LoginName, request?.CurrentPassword, request?.NewPassword, context.RequestAborted).ConfigureAwait(false);
        switch (outcome)
        {
            case PasswordChangeOutcome.Changed:
                limiter.Reset(accountKey);
                limiter.Reset(clientKey);
                return Results.NoContent();
            case PasswordChangeOutcome.InvalidNewPassword:
                return Error(StatusCodes.Status422UnprocessableEntity, "invalid_password", $"The new password needs {AccountRules.MinimumPasswordLength} to {AccountRules.MaximumPasswordLength} printable characters without spaces.");
            default:
                return InvalidCredentials();
        }
    }

    private static async Task<IResult> GetAccountCharactersAsync(string loginName, HttpContext context, IWebsiteStore store, IWebsiteStatusSource status)
    {
        var account = AccountRules.IsValidLoginName(loginName) ? await store.FindAccountAsync(loginName, context.RequestAborted).ConfigureAwait(false) : null;
        if (account is null || !account.CanLogIn)
        {
            return NotFound();
        }

        var characters = await store.GetAccountCharactersAsync(account.Id, context.RequestAborted).ConfigureAwait(false);
        return Results.Ok(characters.Select(c => new
        {
            name = c.Name,
            @class = c.Class,
            level = c.Level,
            resets = c.Resets,
            masterLevel = c.MasterLevel,
            stats = new { str = c.Stats.Str, agi = c.Stats.Agi, vit = c.Stats.Vit, ene = c.Stats.Ene, cmd = c.Stats.Cmd },
            points = c.Points,
            hidden = false,
            online = status.IsCharacterOnline(c.Name),
            pk = c.PkKills,
            pkState = c.PkState,
        }));
    }

    private static async Task<IResult> GetKillersRankingAsync(HttpContext context, IWebsiteStore store)
    {
        var ranking = await store.GetKillersRankingAsync(ParseLimit(context), context.RequestAborted).ConfigureAwait(false);
        return Results.Ok(ranking.Select(e => new { rank = e.Rank, name = e.Name, @class = e.Class, value = e.Value, guild = e.Guild }));
    }

    // Rankings of things the database does not count; the site hides them.
    private static IResult GetUnsupportedRanking(string kind)
    {
        return kind is "online-time" or "gens" or "bc" or "ds" or "cc" or "duel" ? NotSupported() : NotFound();
    }

    private static IResult GetEvents() => NotSupported();

    private static async Task<IResult> GetOnlineAsync(HttpContext context, IWebsiteStore store, IWebsiteStatusSource status)
    {
        var names = (await status.GetOnlineCharacterNamesAsync().ConfigureAwait(false)).Take(MaximumOnlineList * 4).ToList();
        var characters = await store.GetCharactersByNamesAsync(names, context.RequestAborted).ConfigureAwait(false);
        return Results.Ok(characters.Take(MaximumOnlineList).Select(c => new { name = c.Name, @class = c.Class, level = c.Level, guild = c.Guild }));
    }

    // Accounts store no ban reason and no end date, so those are always null.
    private static async Task<IResult> GetBansAsync(HttpContext context, IWebsiteStore store)
    {
        var names = await store.GetBannedCharacterNamesAsync(context.RequestAborted).ConfigureAwait(false);
        return Results.Ok(names.Select(n => new { name = n, reason = (string?)null, until = (DateTimeOffset?)null }));
    }

    private static async Task<IResult> SearchAsync(HttpContext context, IWebsiteStore store)
    {
        string text = context.Request.Query["q"].ToString().Trim();
        if (text.Length is < 2 or > 20 || text.Any(char.IsControl))
        {
            return Error(StatusCodes.Status422UnprocessableEntity, "invalid_input", "The search text needs 2 to 20 characters.");
        }

        var result = await store.SearchAsync(text, context.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new { characters = result.Characters, guilds = result.Guilds });
    }

    // nextSiege is always null: the schedule is a configuration of the game server, not stored with the castle data.
    private static async Task<IResult> GetCastleSiegeAsync(HttpContext context, IWebsiteStore store)
    {
        var info = await store.GetCastleSiegeAsync(context.RequestAborted).ConfigureAwait(false);
        return Results.Ok(new
        {
            owner = info.Owner is { } owner ? new { guild = owner.Guild, master = owner.Master } : null,
            nextSiege = (DateTimeOffset?)null,
            registered = info.Registered.Select(g => new { guild = g.Guild, master = g.Master }),
        });
    }

    private static IResult NotSupported() => Error(StatusCodes.Status404NotFound, "not_supported", "This is not supported by the server.");

    private static async Task<IResult> GetAccountAsync(string loginName, HttpContext context, WebsiteAccountService accounts)
    {
        if (await accounts.GetAccountAsync(loginName, context.RequestAborted).ConfigureAwait(false) is not { } found)
        {
            return NotFound();
        }

        return Results.Ok(new
        {
            loginName = found.Account.LoginName,
            email = found.Details.Email,
            emailVerified = found.Details.EmailVerified,
            registeredAt = found.Details.RegisteredAt,
        });
    }

    private static async Task<IResult> ChangeEmailAsync(HttpContext context, WebsiteAccountService accounts, RateLimiter limiter, WebsiteApiOptions options)
    {
        var request = await ReadJsonAsync<EmailRequest>(context).ConfigureAwait(false);
        var limited = CheckAccountAttemptLimit(request?.LoginName, context, limiter, options, out var accountKey, out var clientKey);
        if (limited is not null)
        {
            return limited;
        }

        var outcome = await accounts.ChangeEmailAsync(request?.LoginName, request?.Password, request?.NewEmail, context.RequestAborted).ConfigureAwait(false);
        switch (outcome)
        {
            case EmailChangeOutcome.Changed:
                limiter.Reset(accountKey);
                limiter.Reset(clientKey);
                return Results.NoContent();
            case EmailChangeOutcome.InvalidEmail:
                return Error(StatusCodes.Status422UnprocessableEntity, "invalid_email", "The e-mail address is not valid.");
            case EmailChangeOutcome.EmailTaken:
                return Error(StatusCodes.Status409Conflict, "email_taken", "This e-mail address is already used.");
            default:
                return InvalidCredentials();
        }
    }

    private static async Task<IResult> RequestEmailVerificationAsync(HttpContext context, WebsiteAccountService accounts, RateLimiter limiter)
    {
        var request = await ReadJsonAsync<LoginNameRequest>(context).ConfigureAwait(false);

        // Counted before the lookup, so unknown names count, too.
        if (!limiter.TryHit("verify-mail:" + (request?.LoginName ?? string.Empty).ToLowerInvariant(), MailTokensPerHour, TimeSpan.FromHours(1)))
        {
            return Error(StatusCodes.Status429TooManyRequests, "rate_limited", "Too many requests, try again later.");
        }

        var (outcome, email, token) = await accounts.RequestEmailVerificationAsync(request?.LoginName, context.RequestAborted).ConfigureAwait(false);
        return outcome switch
        {
            VerificationRequestOutcome.Issued => Results.Ok(new { email, token }),
            VerificationRequestOutcome.AlreadyVerified => Error(StatusCodes.Status409Conflict, "already_verified", "The e-mail address is verified already."),
            VerificationRequestOutcome.NoEmail => Error(StatusCodes.Status422UnprocessableEntity, "invalid_email", "The account has no e-mail address."),
            _ => NotFound(),
        };
    }

    private static async Task<IResult> ConfirmEmailVerificationAsync(HttpContext context, WebsiteAccountService accounts)
    {
        var request = await ReadJsonAsync<TokenRequest>(context).ConfigureAwait(false);
        var loginName = await accounts.ConfirmEmailVerificationAsync(request?.Token, context.RequestAborted).ConfigureAwait(false);
        return loginName is null ? InvalidToken() : Results.Ok(new { loginName });
    }

    private static async Task<IResult> RequestPasswordResetAsync(HttpContext context, WebsiteAccountService accounts, RateLimiter limiter)
    {
        var request = await ReadJsonAsync<ResetRequest>(context).ConfigureAwait(false);

        // Counted before the lookup, so unknown addresses count, too.
        if (!limiter.TryHit("reset-ip:" + WebsiteApiHost.GetClientAddress(context), ResetRequestsPerHourPerClient, TimeSpan.FromHours(1))
            || !limiter.TryHit("reset-mail:" + (request?.Email ?? string.Empty).ToLowerInvariant(), MailTokensPerHour, TimeSpan.FromHours(1)))
        {
            return Error(StatusCodes.Status429TooManyRequests, "rate_limited", "Too many requests, try again later.");
        }

        if (await accounts.RequestPasswordResetAsync(request?.Email, context.RequestAborted).ConfigureAwait(false) is not { } reset)
        {
            return Results.Ok(new { found = false });
        }

        return Results.Ok(new { found = true, loginName = reset.LoginName, email = reset.Email, token = reset.Token });
    }

    private static async Task<IResult> ConfirmPasswordResetAsync(HttpContext context, WebsiteAccountService accounts)
    {
        var request = await ReadJsonAsync<ResetConfirmRequest>(context).ConfigureAwait(false);
        var outcome = await accounts.ConfirmPasswordResetAsync(request?.Token, request?.NewPassword, context.RequestAborted).ConfigureAwait(false);
        return outcome switch
        {
            PasswordResetOutcome.Changed => Results.NoContent(),
            PasswordResetOutcome.InvalidPassword => Error(StatusCodes.Status422UnprocessableEntity, "invalid_password", $"The new password needs {AccountRules.MinimumPasswordLength} to {AccountRules.MaximumPasswordLength} printable characters without spaces."),
            _ => InvalidToken(),
        };
    }

    private static IResult InvalidToken() => Error(StatusCodes.Status422UnprocessableEntity, "invalid_token", "The token is unknown, used or expired.");

    /// <summary>
    /// Counts an attempt to log in. Limited per account name (so one account cannot be guessed at) and per client address.
    /// Both counters are cleared by a success.
    /// </summary>
    private static IResult? CheckAccountAttemptLimit(string? loginName, HttpContext context, RateLimiter limiter, WebsiteApiOptions options, out string accountKey, out string clientKey)
    {
        accountKey = "acct:" + (loginName ?? string.Empty).ToLowerInvariant();
        clientKey = "login-client:" + WebsiteApiHost.GetClientAddress(context);
        var window = TimeSpan.FromMinutes(10);
        if (!limiter.TryHit(accountKey, options.AuthAttemptsPer10MinutesPerAccount, window)
            || !limiter.TryHit(clientKey, options.AuthAttemptsPer10MinutesPerAccount * 5, window))
        {
            return Error(StatusCodes.Status429TooManyRequests, "rate_limited", "Too many attempts, try again later.");
        }

        return null;
    }

    private static int ParseLimit(HttpContext context)
    {
        if (int.TryParse(context.Request.Query["limit"], out var limit))
        {
            return Math.Clamp(limit, 1, MaximumRankingLimit);
        }

        return DefaultRankingLimit;
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpContext context)
        where T : class
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(context.Request.Body, JsonOptions, context.RequestAborted).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IResult NotFound() => Error(StatusCodes.Status404NotFound, "not_found", "Not found.");

    private static IResult InvalidCredentials() => Error(StatusCodes.Status401Unauthorized, "invalid_credentials", "The name or the password is wrong.");

    private static IResult Error(int status, string code, string message)
        => Results.Json(new { error = code, message }, statusCode: status);

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
    };

    private sealed record RegisterRequest(string? LoginName, string? Password, string? Email);

    private sealed record VerifyRequest(string? LoginName, string? Password);

    private sealed record EmailRequest(string? LoginName, string? Password, string? NewEmail);

    private sealed record LoginNameRequest(string? LoginName);

    private sealed record TokenRequest(string? Token);

    private sealed record ResetRequest(string? Email);

    private sealed record ResetConfirmRequest(string? Token, string? NewPassword);

    private sealed record PasswordRequest(string? LoginName, string? CurrentPassword, string? NewPassword);
}
