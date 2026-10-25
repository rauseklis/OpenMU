// <copyright file="ReusableEventTicketDatabaseTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
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
/// Exercises ticket upgrades only on the task-owned disposable PostgreSQL cluster.
/// </summary>
[TestFixture]
public class ReusableEventTicketDatabaseTests
{
    private static readonly string DatabaseName = $"mu_reusable_tickets_{Guid.NewGuid():N}";
    private static readonly string Connection = $"Host=127.0.0.1;Port=55483;Database={DatabaseName};User Id=ribbonvalidation;Command Timeout=120;";

    [Test]
    [Explicit("Requires REUSABLE_TICKET_FIXTURE=seed-disposable on task-owned 127.0.0.1:55483.")]
    public async Task UpgradeAndEditedUseCountsPersistAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("REUSABLE_TICKET_FIXTURE"), Is.EqualTo("seed-disposable"));
        Assert.That(ConnectionConfigurator.IsInitialized, Is.False);
        await EnsureFreshDatabaseAsync().ConfigureAwait(false);
        ConnectionConfigurator.Initialize(new FixtureConnectionSettings());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        await provider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        await SimulateMissingStockAsync(provider).ConfigureAwait(false);
        Assert.That(await ReusableEventTicketsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await ReusableEventTicketsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        await EditAndReloadStockAsync(provider).ConfigureAwait(false);
        await using var database = new EntityDataContext();
        Assert.That(database.Database.HasPendingModelChanges(), Is.False);
        await database.Database.ExecuteSqlRawAsync("""
            UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-20000;
            UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-20000;
            UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-20000;
            """).ConfigureAwait(false);
        TestContext.Progress.WriteLine($"Disposable ticket database: {DatabaseName}");
    }

    private static async Task EnsureFreshDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection("Host=127.0.0.1;Port=55483;Database=postgres;User Id=ribbonvalidation;");
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", connection);
        command.Parameters.AddWithValue("name", DatabaseName);
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false)), Is.Zero);
    }

    private static async Task SimulateMissingStockAsync(IPersistenceContextProvider provider)
    {
        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var shop = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        foreach (var item in Tickets(shop).ToArray())
        {
            shop.Items.Remove(item);
            await context.DeleteAsync(item).ConfigureAwait(false);
        }

        foreach (var definition in configuration.Items.Where(item => item.Group == 13 && item.Number is >= 46 and <= 48).ToArray())
        {
            configuration.Items.Remove(definition);
            await context.DeleteAsync(definition).ConfigureAwait(false);
        }

        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(update => update.Version == (int)UpdateVersion.ReusableEventTickets).ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task EditAndReloadStockAsync(IPersistenceContextProvider provider)
    {
        int[] uses = [1, 5, 10];
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var shop = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
            var tickets = Tickets(shop).OrderBy(item => item.Definition!.Number).ToArray();
            Assert.That(tickets, Has.Length.EqualTo(3));
            Assert.That(tickets.All(item => item.Durability == 10 && item.StorePrice == 1000 && !item.Definition!.IsRepairable), Is.True);
            for (var index = 0; index < tickets.Length; index++)
            {
                tickets[index].Durability = uses[index];
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await ReusableEventTicketsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using var reloaded = provider.CreateNewContext();
        var persisted = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var persistedShop = persisted.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
        Assert.That(Tickets(persistedShop).OrderBy(item => item.Definition!.Number).Select(item => item.Durability), Is.EqualTo(uses));
    }

    private static IEnumerable<Item> Tickets(ItemStorage shop)
        => shop.Items.Where(item => item.Definition is { Group: 13, Number: >= 46 and <= 48 });

    private sealed class FixtureConnectionSettings : IDatabaseConnectionSettingProvider
    {
        public Task? Initialization => null;

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ConnectionSetting GetConnectionSetting<TContextType>() where TContextType : DbContext
            => this.GetConnectionSetting(typeof(TContextType));

        public ConnectionSetting GetConnectionSetting(Type contextType) => new()
        {
            ContextTypeName = contextType.FullName,
            ConnectionString = Connection,
            DatabaseEngine = DatabaseEngine.Npgsql,
        };
    }
}
