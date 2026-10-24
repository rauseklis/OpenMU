// <copyright file="TierOneAncientStatsDatabaseTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using Npgsql;

/// <summary>
/// Validates the configuration upgrade only in the task-owned loopback cluster.
/// </summary>
[TestFixture]
public class TierOneAncientStatsDatabaseTests
{
    private const string FixtureConnection = "Host=127.0.0.1;Port=55473;Database=mu_tier1_full_stats;User Id=tier1validation;Command Timeout=120;";

    /// <summary>
    /// Seeds a new disposable database, simulates the previous options, and persists the upgrade once.
    /// </summary>
    [Test]
    [Explicit("Requires TIER1_STATS_FIXTURE=seed-disposable and the task-owned PostgreSQL cluster on 127.0.0.1:55473.")]
    public async Task UpgradePreservesOriginalOptionsAndRestartsAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("TIER1_STATS_FIXTURE"), Is.EqualTo("seed-disposable"));
        Assert.That(ConnectionConfigurator.IsInitialized, Is.False);
        await using (var connection = new NpgsqlConnection("Host=127.0.0.1;Port=55473;Database=postgres;User Id=tier1validation;"))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname='mu_tier1_full_stats'", connection);
            Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false)), Is.Zero, "Refusing to overwrite an existing fixture.");
        }

        ConnectionConfigurator.Initialize(new FixtureConnectionSettings());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        await provider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        await using (var database = new EntityDataContext())
        {
            await database.Database.ExecuteSqlRawAsync("""
                UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-20000;
                UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-20000;
                UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-20000;
                DELETE FROM config."IncreasableItemOption" o USING config."ItemSetGroup" s
                WHERE o."ItemOptionDefinitionId"=s."OptionsId" AND
                  ((s."Name" IN ('Hyperion','Barnake') AND o."Number">4) OR
                   (s."Name"='Fase' AND o."Number">3) OR (s."Name"='Cloud' AND o."Number">2));
                """).ConfigureAwait(false);
        }

        Guid[] originals;
        using (var before = provider.CreateNewContext())
        {
            var configuration = (await before.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            originals = configuration.ItemSetGroups.SelectMany(set => set.Options?.PossibleOptions ?? [])
                .Select(option => option.GetId()).ToArray();
        }

        Assert.That(await TierOneAncientStatsUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await TierOneAncientStatsUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using var reloaded = provider.CreateNewContext();
        var persisted = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var allOptions = persisted.ItemSetGroups.SelectMany(set => set.Options?.PossibleOptions ?? []).Select(option => option.GetId()).ToArray();
        Assert.That(originals.All(allOptions.Contains), Is.True);
        Assert.That(allOptions.Length, Is.EqualTo(originals.Length + 16));
        foreach (var entry in new[] { (Name: "Hyperion", Count: 8), (Name: "Barnake", Count: 8), (Name: "Fase", Count: 7), (Name: "Cloud", Count: 6) })
        {
            Assert.That(persisted.ItemSetGroups.Single(set => set.Name == entry.Name).Options!.PossibleOptions.Count, Is.EqualTo(entry.Count));
        }
    }

    private sealed class FixtureConnectionSettings : IDatabaseConnectionSettingProvider
    {
        public Task? Initialization => null;

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ConnectionSetting GetConnectionSetting<TContextType>() where TContextType : DbContext
            => this.GetConnectionSetting(typeof(TContextType));

        public ConnectionSetting GetConnectionSetting(Type contextType) => new()
        {
            ContextTypeName = contextType.FullName,
            ConnectionString = FixtureConnection,
            DatabaseEngine = DatabaseEngine.Npgsql,
        };
    }
}
