// <copyright file="WebsiteApiDatabase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Tests;

using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

/// <summary>
/// Creates and fills a disposable PostgreSQL database for the integration tests.
/// The connection string comes from <c>WEBSITE_API_TEST_CONNECTION_STRING</c>; without it the tests are skipped.
/// The database is dropped and created again, so the name has to contain "test" - a safeguard against pointing it at a real one.
/// </summary>
public static class WebsiteApiDatabase
{
    /// <summary>The environment variable with the connection string of the disposable database.</summary>
    public const string ConnectionStringVariable = "WEBSITE_API_TEST_CONNECTION_STRING";

    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static bool _initialized;

    /// <summary>
    /// Gets the connection string, or <c>null</c> when the integration tests are not configured.
    /// </summary>
    public static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionStringVariable);

    /// <summary>
    /// Makes sure the database exists with the seeded game data; skips the test when it is not configured.
    /// </summary>
    /// <returns>The task.</returns>
    public static async Task EnsureAsync()
    {
        var connectionString = ConnectionString;
        if (connectionString is null)
        {
            Assert.Ignore($"Set {ConnectionStringVariable} to a throwaway PostgreSQL database to run the integration tests.");
        }

        var database = new NpgsqlConnectionStringBuilder(connectionString).Database ?? string.Empty;
        if (!database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to recreate the database '{database}': its name has to contain 'test'.");
        }

        await Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
            JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
            ConnectionConfigurator.Initialize(new FixedConnectionSettingProvider(connectionString!));
            var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
            using (await provider.ReCreateDatabaseAsync().ConfigureAwait(false))
            {
                await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, true).ConfigureAwait(false);
            }

            _initialized = true;
        }
        finally
        {
            Lock.Release();
        }
    }

    private sealed class FixedConnectionSettingProvider : IDatabaseConnectionSettingProvider
    {
        private readonly string _connectionString;

        public FixedConnectionSettingProvider(string connectionString)
        {
            this._connectionString = connectionString;
        }

        public Task? Initialization => null;

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ConnectionSetting GetConnectionSetting<TContextType>()
            where TContextType : Microsoft.EntityFrameworkCore.DbContext
            => this.GetConnectionSetting(typeof(TContextType));

        public ConnectionSetting GetConnectionSetting(Type contextType)
            => new()
            {
                ContextTypeName = contextType.FullName,
                ConnectionString = this._connectionString,
                DatabaseEngine = DatabaseEngine.Npgsql,
            };
    }
}
