// <copyright file="TierTwoAncientStatsDatabaseTests.cs" company="MUnique">
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
public class TierTwoAncientStatsDatabaseTests
{
    private static readonly string FixtureDatabase = $"mu_tier2_full_stats_{Guid.NewGuid():N}";

    private static readonly string FixtureConnection = $"Host=127.0.0.1;Port=55473;Database={FixtureDatabase};User Id=tier1validation;Command Timeout=120;";

    /// <summary>
    /// Seeds a new disposable database, simulates the previous options, and persists the upgrade once.
    /// </summary>
    [Test]
    [Explicit("Requires TIER2_STATS_FIXTURE=seed-disposable and the task-owned PostgreSQL cluster on 127.0.0.1:55473.")]
    public async Task UpgradePreservesOriginalOptionsAndRestartsAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("TIER2_STATS_FIXTURE"), Is.EqualTo("seed-disposable"));
        Assert.That(ConnectionConfigurator.IsInitialized, Is.False);
        await using (var connection = new NpgsqlConnection("Host=127.0.0.1;Port=55473;Database=postgres;User Id=tier1validation;"))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@database", connection);
            command.Parameters.AddWithValue("database", FixtureDatabase);
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
                DELETE FROM config."ConfigurationUpdate" WHERE "Version"=132;
                UPDATE config."ConfigurationUpdateState" SET "CurrentInstalledVersion"=131;
                UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-20000;
                UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-20000;
                UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-20000;
                DELETE FROM config."IncreasableItemOption" o USING config."ItemSetGroup" s
                WHERE o."ItemOptionDefinitionId"=s."OptionsId" AND
                  ((s."Name" IN ('Garuda','Semeden') AND o."Number">6) OR
                   (s."Name" IN ('Gaia','Eplete','Chamer') AND o."Number">7) OR
                   (s."Name"='Heras' AND o."Number">9));
                """).ConfigureAwait(false);
        }

        Guid[] originals;
        using (var before = provider.CreateNewContext())
        {
            var configuration = (await before.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            originals = configuration.ItemSetGroups.SelectMany(set => set.Options?.PossibleOptions ?? [])
                .Select(option => option.GetId()).ToArray();
        }

        Assert.That(await TierTwoAncientStatsUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await TierTwoAncientStatsUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using var reloaded = provider.CreateNewContext();
        var persisted = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var allOptions = persisted.ItemSetGroups.SelectMany(set => set.Options?.PossibleOptions ?? []).Select(option => option.GetId()).ToArray();
        Assert.That(originals.All(allOptions.Contains), Is.True);
        Assert.That(allOptions.Length, Is.EqualTo(originals.Length + 24));
        foreach (var entry in new[] { (Name: "Garuda", Count: 10), (Name: "Heras", Count: 13), (Name: "Gaia", Count: 11), (Name: "Eplete", Count: 11), (Name: "Semeden", Count: 10), (Name: "Chamer", Count: 11) })
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
