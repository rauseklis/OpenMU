// <copyright file="WebsiteApiService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.WebsiteApi.Data;

/// <summary>
/// Runs the website API next to the game servers, when it is configured. A failure to start (for example a port in use or
/// an unreadable certificate) is logged, but never stops the game server.
/// </summary>
public sealed class WebsiteApiService : IHostedService, IAsyncDisposable
{
    private readonly WebsiteApiOptions _options;
    private readonly IWebsiteStatusSource _status;
    private readonly ILoggerFactory _loggerFactory;
    private readonly PostgresWebsiteStore _store;
    private WebApplication? _app;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebsiteApiService"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="status">The status source.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public WebsiteApiService(WebsiteApiOptions options, IWebsiteStatusSource status, ILoggerFactory loggerFactory)
    {
        this._options = options;
        this._status = status;
        this._loggerFactory = loggerFactory;
        this._store = new PostgresWebsiteStore(options.ReadConnectionString);
    }

    /// <summary>
    /// Registers the service when the environment configures the website API; otherwise only logs why it is off.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="statusSourceFactory">Creates the status source from the service provider.</param>
    /// <param name="log">Receives the messages about the configuration.</param>
    /// <returns><c>true</c> if the API was registered.</returns>
    public static bool AddIfConfigured(IServiceCollection services, Func<IServiceProvider, IWebsiteStatusSource> statusSourceFactory, Action<string> log)
    {
        var options = WebsiteApiOptions.FromEnvironment(Environment.GetEnvironmentVariable, out var error);
        if (options is null)
        {
            if (error is not null)
            {
                log($"The website API is configured wrongly and stays off: {error}");
            }

            return false;
        }

        services.AddSingleton(options);
        services.AddSingleton<IWebsiteStatusSource>(statusSourceFactory);
        services.AddHostedService<WebsiteApiService>();
        log($"The website API will listen on {options.EndPoint} ({(options.CertificatePath is null ? "plain HTTP, tests only" : "TLS")}).");
        return true;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var logger = this._loggerFactory.CreateLogger<WebsiteApiService>();
        try
        {
            this._app = WebsiteApiHost.Build(this._options, this._store, this._status, this._loggerFactory);
            await this._app.StartAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Website API started on {EndPoint}.", this._options.EndPoint);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The website API could not be started; the game server runs without it.");
            this._app = null;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (this._app is { } app)
        {
            await app.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this._app is { } app)
        {
            await app.DisposeAsync().ConfigureAwait(false);
        }

        await this._store.DisposeAsync().ConfigureAwait(false);
    }
}
