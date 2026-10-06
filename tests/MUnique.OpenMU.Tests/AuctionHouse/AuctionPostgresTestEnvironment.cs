// <copyright file="AuctionPostgresTestEnvironment.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.Threading;
using MUnique.OpenMU.Persistence.EntityFramework;

/// <summary>
/// Connects the Entity Framework context to a disposable PostgreSQL database for the Auction House integration tests.
/// The connection string is read from the <c>AUCTION_TEST_CONNECTION_STRING</c> environment variable. Without it,
/// the integration tests are skipped, so the default suite never touches a database.
/// </summary>
public static class AuctionPostgresTestEnvironment
{
    /// <summary>
    /// The environment variable which holds the disposable database connection string.
    /// </summary>
    public const string ConnectionStringVariable = "AUCTION_TEST_CONNECTION_STRING";

    /// <summary>
    /// Gets the connection string, or <c>null</c> when the integration tests are not configured.
    /// </summary>
    public static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionStringVariable);

    /// <summary>
    /// Makes the entity framework context use the disposable database. The configurator can be initialized only once per
    /// process, so this is idempotent.
    /// </summary>
    public static void EnsureConfigured()
    {
        if (ConnectionConfigurator.IsInitialized)
        {
            return;
        }

        ConnectionConfigurator.Initialize(new DisposableDatabaseSettingProvider(ConnectionString!));
    }

    private sealed class DisposableDatabaseSettingProvider : IDatabaseConnectionSettingProvider
    {
        private readonly string _connectionString;

        public DisposableDatabaseSettingProvider(string connectionString)
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
