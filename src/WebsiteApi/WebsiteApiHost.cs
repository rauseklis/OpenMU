// <copyright file="WebsiteApiHost.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi;

using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.WebsiteApi.Accounts;
using MUnique.OpenMU.WebsiteApi.Data;
using MUnique.OpenMU.WebsiteApi.Security;

/// <summary>
/// Builds the small web host of the website API: its own Kestrel listener on its own port (the admin panel is not touched),
/// the request authentication and the endpoints of <see cref="WebsiteApiEndpoints"/>.
/// </summary>
public static class WebsiteApiHost
{
    /// <summary>The maximum size of a request body in bytes.</summary>
    public const int MaximumBodySize = 8 * 1024;

    private const string ItemApiError = "WebsiteApi.Error";

    /// <summary>
    /// Creates the web application. It is not started.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="store">The data store.</param>
    /// <param name="status">The status source.</param>
    /// <param name="loggerFactory">The logger factory of the host.</param>
    /// <param name="clock">The clock; defaults to the system clock.</param>
    /// <returns>The application.</returns>
    public static WebApplication Build(WebsiteApiOptions options, IWebsiteStore store, IWebsiteStatusSource status, ILoggerFactory loggerFactory, Func<DateTimeOffset>? clock = null)
    {
        clock ??= () => DateTimeOffset.UtcNow;
        var builder = WebApplication.CreateSlimBuilder();

        // The admin panel's ASPNETCORE_URLS must not leak into this host.
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, string.Empty);
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = MaximumBodySize;
            kestrel.Listen(options.EndPoint, listen =>
            {
                if (options.CertificatePath is { } path)
                {
                    listen.UseHttps(X509CertificateLoader.LoadPkcs12FromFile(path, options.CertificatePassword));
                }
            });
        });

        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(loggerFactory);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(status);
        builder.Services.AddSingleton(new WebsiteAccountService(store, clock));
        builder.Services.AddSingleton<ReplayCache>();
        builder.Services.AddSingleton(new RateLimiter(clock));
        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            json.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never;
        });

        var app = builder.Build();
        var logger = loggerFactory.CreateLogger("MUnique.OpenMU.WebsiteApi");

        app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (BadHttpRequestException ex)
            {
                if (!context.Response.HasStarted)
                {
                    await WriteErrorAsync(context, ex.StatusCode, "bad_request", "The request could not be read.").ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unhandled error in the website API for {Method} {Path}.", context.Request.Method, context.Request.Path);
                if (!context.Response.HasStarted)
                {
                    await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "server_error", "Something went wrong.").ConfigureAwait(false);
                }
            }
        });

        app.Use((context, next) => AuthenticateAsync(context, next, options, app.Services.GetRequiredService<ReplayCache>(), app.Services.GetRequiredService<RateLimiter>(), clock, logger));
        WebsiteApiEndpoints.Map(app);
        app.MapFallback(context => WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found", "No such endpoint."));
        return app;
    }

    /// <summary>
    /// Gets the address of the caller for rate limiting: the <c>X-Client-Ip</c> header the website adds (only trusted
    /// because the request is signed), else the address of the connection.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The address text.</returns>
    public static string GetClientAddress(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Client-Ip", out var header)
            && IPAddress.TryParse(header.ToString(), out var forwarded))
        {
            return (forwarded.IsIPv4MappedToIPv6 ? forwarded.MapToIPv4() : forwarded).ToString();
        }

        return GetConnectionAddress(context);
    }

    /// <summary>
    /// Writes the JSON error response <c>{ "error": code, "message": text }</c>.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="status">The HTTP status.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The message.</param>
    /// <returns>The task.</returns>
    public static Task WriteErrorAsync(HttpContext context, int status, string code, string message)
    {
        context.Items[ItemApiError] = code;
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { error = code, message });
    }

    private static string GetConnectionAddress(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress ?? IPAddress.None;
        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    private static async Task AuthenticateAsync(HttpContext context, RequestDelegate next, WebsiteApiOptions options, ReplayCache replayCache, RateLimiter limiter, Func<DateTimeOffset> clock, ILogger logger)
    {
        var connectionAddress = GetConnectionAddress(context);
        if (options.AllowedIps.Count > 0 && !options.AllowedIps.Any(ip => ip.ToString() == connectionAddress))
        {
            logger.LogWarning("Rejected a request from {Address}: not in {Variable}.", connectionAddress, WebsiteApiOptions.AllowedIpsVariableName);
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "forbidden", "This address may not use the API.").ConfigureAwait(false);
            return;
        }

        // Too many failures from one connection: stop even looking at the signature for a while.
        if (!limiter.TryHit("conn-total:" + connectionAddress, 600, TimeSpan.FromMinutes(1)))
        {
            await WriteErrorAsync(context, StatusCodes.Status429TooManyRequests, "rate_limited", "Too many requests.").ConfigureAwait(false);
            return;
        }

        var failureKey = "auth-fail:" + connectionAddress;
        var failureError = await CheckSignatureAsync(context, options, replayCache, clock).ConfigureAwait(false);
        if (failureError is { } error)
        {
            if (!limiter.TryHit(failureKey, 20, TimeSpan.FromMinutes(10)))
            {
                await WriteErrorAsync(context, StatusCodes.Status429TooManyRequests, "rate_limited", "Too many failed requests.").ConfigureAwait(false);
                return;
            }

            logger.LogWarning("Rejected a request from {Address}: {Error}.", connectionAddress, error.Code);
            await WriteErrorAsync(context, error.Status, error.Code, error.Message).ConfigureAwait(false);
            return;
        }

        if (!limiter.TryHit("ip:" + GetClientAddress(context), options.RequestsPerMinutePerIp, TimeSpan.FromMinutes(1)))
        {
            await WriteErrorAsync(context, StatusCodes.Status429TooManyRequests, "rate_limited", "Too many requests.").ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static async Task<(int Status, string Code, string Message)?> CheckSignatureAsync(HttpContext context, WebsiteApiOptions options, ReplayCache replayCache, Func<DateTimeOffset> clock)
    {
        var request = context.Request;
        string? keyId = request.Headers["X-Key-Id"];
        string? timestamp = request.Headers["X-Timestamp"];
        string? signature = request.Headers["X-Signature"];
        if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
        {
            return (StatusCodes.Status401Unauthorized, "bad_signature", "The signature headers are missing.");
        }

        var now = clock();
        if (!RequestSigner.IsFresh(timestamp, now))
        {
            return (StatusCodes.Status401Unauthorized, "stale_timestamp", "The timestamp is outside of the allowed window.");
        }

        // The body has to be read completely to sign it; the size is limited by Kestrel and checked here for chunked bodies.
        request.EnableBuffering();
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk).ConfigureAwait(false)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read)).ConfigureAwait(false);
            if (buffer.Length > MaximumBodySize)
            {
                return (StatusCodes.Status413PayloadTooLarge, "bad_request", "The request body is too large.");
            }
        }

        var body = buffer.ToArray();
        request.Body.Position = 0;

        // The path and query exactly as they were sent.
        var target = context.Features.Get<IHttpRequestFeature>()?.RawTarget ?? (request.Path + request.QueryString);
        if (!options.Keys.TryGetValue(keyId, out var secret))
        {
            return (StatusCodes.Status401Unauthorized, "bad_signature", "The signature is wrong.");
        }

        var expected = RequestSigner.Sign(secret, request.Method, target, timestamp, body);
        if (!RequestSigner.AreEqual(expected, signature))
        {
            return (StatusCodes.Status401Unauthorized, "bad_signature", "The signature is wrong.");
        }

        // A repeated read request is harmless (and a page which asks for the same list twice in one second is normal),
        // a repeated change is not: only requests which change something are refused when seen twice.
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !replayCache.TryRegister(expected, now))
        {
            return (StatusCodes.Status401Unauthorized, "bad_signature", "This request was already used.");
        }

        return null;
    }
}
