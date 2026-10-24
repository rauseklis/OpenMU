// <copyright file="RibbonBoxShopDatabaseTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using Npgsql;

/// <summary>
/// Tests persistence only on the task's disposable PostgreSQL cluster.
/// </summary>
[TestFixture]
public class RibbonBoxShopDatabaseTests
{
    private const string FixtureDatabase = "mu_ribbon_shop_20261024";
    private const string FixtureConnection = "Host=127.0.0.1;Port=55483;Database=" + FixtureDatabase + ";User Id=ribbonvalidation;Command Timeout=120;";

    /// <summary>
    /// Seeds a disposable database and upgrades existing shop stock without changing it.
    /// </summary>
    [Test]
    [Explicit("Requires RIBBON_SHOP_FIXTURE=seed-disposable and the task-owned cluster at 127.0.0.1:55483.")]
    public async Task UpgradePersistsListingsAndRunsOnceAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("RIBBON_SHOP_FIXTURE"), Is.EqualTo("seed-disposable"));
        Assert.That(ConnectionConfigurator.IsInitialized, Is.False);
        await using (var connection = new NpgsqlConnection("Host=127.0.0.1;Port=55483;Database=postgres;User Id=ribbonvalidation;"))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", connection);
            command.Parameters.AddWithValue("name", FixtureDatabase);
            Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false)), Is.Zero, "Refusing to overwrite a fixture.");
        }

        ConnectionConfigurator.Initialize(new FixtureConnectionSettings());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        await provider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var store = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
            foreach (var listing in store.Items.Where(item => item.Definition is { Group: 12, Number: >= 32 and <= 34 }).ToArray())
            {
                store.Items.Remove(listing);
                await context.DeleteAsync(listing).ConfigureAwait(false);
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(update => update.Version == (int)UpdateVersion.RibbonBoxShop))
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await RibbonBoxShopUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await RibbonBoxShopUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using var reloaded = provider.CreateNewContext();
        var persisted = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var persistedStore = persisted.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        foreach (var number in new short[] { 32, 33, 34 })
        {
            var listing = persistedStore.Items.Single(item => item.Definition is { Group: 12 } definition && definition.Number == number);
            Assert.That(new ItemPriceCalculator().CalculateMerchantBuyingPrice(listing), Is.EqualTo(1000));
        }

        await using var database = new EntityDataContext();
        await database.Database.ExecuteSqlRawAsync("""
            UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-20000;
            UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-20000;
            UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-20000;
            """).ConfigureAwait(false);
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
