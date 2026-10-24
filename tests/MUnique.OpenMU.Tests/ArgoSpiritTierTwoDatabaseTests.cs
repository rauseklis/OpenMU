// <copyright file="ArgoSpiritTierTwoDatabaseTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration.Items;
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
public class ArgoSpiritTierTwoDatabaseTests
{
    private static readonly string FixtureDatabase = $"mu_argo_tier2_{Guid.NewGuid():N}";

    private static readonly string FixtureConnection = $"Host=127.0.0.1;Port=55473;Database={FixtureDatabase};User Id=tier1validation;Command Timeout=120;";

    /// <summary>
    /// Seeds a new disposable database, simulates the previous options, and persists the upgrade once.
    /// </summary>
    [Test]
    [Explicit("Requires ARGO_STATS_FIXTURE=seed-disposable and the task-owned PostgreSQL cluster on 127.0.0.1:55473.")]
    public async Task UpgradePreservesOriginalOptionsAndRestartsAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("ARGO_STATS_FIXTURE"), Is.EqualTo("seed-disposable"));
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
        using (var oldState = provider.CreateNewContext())
        {
            var config = (await oldState.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var argo = config.ItemSetGroups.Single(set => set.Name == "Argo");
            var gaia = config.ItemSetGroups.Single(set => set.Name == "Gaia");
            foreach (var source in argo.Options!.PossibleOptions.Where(option => option.Number > 4).ToArray())
            {
                var option = oldState.CreateNew<IncreasableItemOption>();
                option.Number = gaia.Options!.PossibleOptions.Count + 1;
                option.OptionType = source.OptionType;
                option.PowerUpDefinition = oldState.CreateNew<PowerUpDefinition>();
                option.PowerUpDefinition.TargetAttribute = source.PowerUpDefinition!.TargetAttribute;
                option.PowerUpDefinition.Boost = oldState.CreateNew<PowerUpDefinitionValue>();
                option.PowerUpDefinition.Boost.ConstantValue.Value = source.PowerUpDefinition.Boost!.ConstantValue.Value;
                option.PowerUpDefinition.Boost.ConstantValue.AggregateType = source.PowerUpDefinition.Boost.ConstantValue.AggregateType;
                gaia.Options.PossibleOptions.Add(option);
            }
            var silver = config.Items.Single(item => item.Group == 14 && item.Number == 124);
            var previous = silver.DropItems.Single(group => group.AncientSet == argo);
            previous.AncientSet = gaia;
            previous.PossibleItems.Clear();
            foreach (var member in gaia.Items)
            {
                previous.PossibleItems.Add(member.ItemDefinition!);
            }
            foreach (var group in silver.DropItems.Where(group => group.SourceItemLevel == 0))
            {
                group.Chance = group.PossibleItems.Count / 29d;
            }
            await oldState.SaveChangesAsync().ConfigureAwait(false);
        }
        await using (var database = new EntityDataContext())
        {
            await database.Database.ExecuteSqlRawAsync("""
                DELETE FROM config."ConfigurationUpdate" WHERE "Version"=133;
                UPDATE config."ConfigurationUpdateState" SET "CurrentInstalledVersion"=132;
                UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-20000;
                UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-20000;
                UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-20000;
                DELETE FROM config."IncreasableItemOption" o USING config."ItemSetGroup" s
                WHERE o."ItemOptionDefinitionId"=s."OptionsId" AND s."Name"='Argo' AND o."Number">4;
                """).ConfigureAwait(false);
        }

        Guid[] originals;
        using (var before = provider.CreateNewContext())
        {
            var configuration = (await before.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            originals = configuration.ItemSetGroups.SelectMany(set => (set.Options?.PossibleOptions ?? []).Where(option => set.Name != "Gaia" || option.Number <= 7))
                .Select(option => option.GetId()).ToArray();
        }

        Assert.That(await ArgoSpiritTierTwoUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await ArgoSpiritTierTwoUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using var reloaded = provider.CreateNewContext();
        var persisted = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var allOptions = persisted.ItemSetGroups.SelectMany(set => set.Options?.PossibleOptions ?? []).Select(option => option.GetId()).ToArray();
        Assert.That(originals.All(allOptions.Contains), Is.True);
        Assert.That(allOptions.Length, Is.EqualTo(originals.Length + 4));
        Assert.That(persisted.ItemSetGroups.Single(set => set.Name == "Gaia").Options!.PossibleOptions.Count, Is.EqualTo(7));
        Assert.That(persisted.ItemSetGroups.Single(set => set.Name == "Argo").Options!.PossibleOptions.Count, Is.EqualTo(8));
        var silverGroups = persisted.Items.Single(item => item.Group == 14 && item.Number == 124).DropItems.Where(group => group.SourceItemLevel == 0).ToArray();
        Assert.That(silverGroups.Any(group => group.AncientSet!.Name == "Gaia"), Is.False);
        Assert.That(silverGroups.Single(group => group.AncientSet!.Name == "Argo").PossibleItems.Count, Is.EqualTo(3));
        Assert.That(silverGroups.Sum(group => group.PossibleItems.Count), Is.EqualTo(27));
        Assert.That(silverGroups.Sum(group => group.Chance), Is.EqualTo(1).Within(0.000000001));
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
