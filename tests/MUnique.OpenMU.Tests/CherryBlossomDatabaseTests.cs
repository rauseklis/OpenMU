// <copyright file="CherryBlossomDatabaseTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using Npgsql;

/// <summary>
/// Verifies persistent Cherry Blossom updates only on a dedicated disposable cluster.
/// </summary>
[TestFixture]
public class CherryBlossomDatabaseTests
{
    private const string DatabaseName = "mu_cherry_blossom_20261025";
    private const string Connection = "Host=127.0.0.1;Port=55493;Database=" + DatabaseName + ";User Id=cherryvalidation;Command Timeout=120;";

    [Test]
    [Explicit("Requires CHERRY_BLOSSOM_FIXTURE=seed-disposable and the task-owned 127.0.0.1:55493 cluster.")]
    public async Task SeedUpgradeReloadAndShiftPortsAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("CHERRY_BLOSSOM_FIXTURE"), Is.EqualTo("seed-disposable"));
        Assert.That(ConnectionConfigurator.IsInitialized, Is.False);
        await using (var connection = new NpgsqlConnection("Host=127.0.0.1;Port=55493;Database=postgres;User Id=cherryvalidation;"))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", connection);
            command.Parameters.AddWithValue("name", DatabaseName);
            Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false)), Is.Zero);
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
            var spirit = configuration.Monsters.Single(monster => monster.Number == 450);
            foreach (var recipe in spirit.ItemCraftings.Where(recipe => recipe.Number is 42 or 43).ToArray())
            {
                spirit.ItemCraftings.Remove(recipe);
            }

            var box = configuration.Items.Single(item => item.Group == 14 && item.Number == 84);
            var group = box.DropItems.Single(drop => drop.SourceItemLevel == 0);
            foreach (var branch in group.PossibleItems.Where(item => item.Number is 88 or 89).ToArray())
            {
                group.PossibleItems.Remove(branch);
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                .Where(update => update.Version == (int)UpdateVersion.CherryBlossomRewards).ToArray())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await CherryBlossomRewardsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await CherryBlossomRewardsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using (var reloaded = provider.CreateNewConfigurationContext())
        {
            var configuration = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var spirit = configuration.Monsters.Single(monster => monster.Number == 450);
            Assert.That(spirit.ItemCraftings.Where(recipe => recipe.Number is 41 or 42 or 43), Has.Exactly(3).Items);
            Assert.That(configuration.Items.Single(item => item.Group == 14 && item.Number == 84).DropItems
                .Single(group => group.SourceItemLevel == 0).PossibleItems.Select(item => item.Number),
                Is.EquivalentTo(new short[] { 85, 86, 87, 88, 89, 90 }));
            var shop = configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
            Assert.That(shop.Items.Any(item => item.Definition is { Group: 13, Number: 16 or 17 }
                or { Group: 14, Number: 17 or 18 }), Is.False);
            foreach (var (number, count) in new[] { (88, 10), (89, 30), (90, 255) })
            {
                var listing = shop.Items.Single(item => item.Definition is { Group: 14 } definition && definition.Number == number);
                Assert.That(listing.Durability, Is.EqualTo(count));
                Assert.That(listing.StorePrice, Is.EqualTo(1000));
            }
            var gold = spirit.ItemCraftings.Single(recipe => recipe.Number == 41).SimpleCraftingSettings!;
            Assert.That(gold.ResultItems, Has.Count.EqualTo(6));
            Assert.That(gold.ResultItems.Single(result => result.ItemDefinition is { Group: 14, Number: 11 }).RandomMinimumLevel, Is.EqualTo(10));
            Assert.That(gold.ResultItems.Any(result => result.ItemDefinition is { Group: 14, Number: 123 }), Is.False);
            Assert.That(gold.ResultItems.Where(result => result.ItemDefinition is { Group: 13 }).All(result => result.Durability == 10), Is.True);
        }

        await using var database = new EntityDataContext();
        Assert.That(database.Database.HasPendingModelChanges(), Is.False);
        await database.Database.ExecuteSqlRawAsync("""
            UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-13000;
            UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-13000;
            UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-13000;
            """).ConfigureAwait(false);
        TestContext.Progress.WriteLine($"Disposable Cherry Blossom database: {DatabaseName}, PostgreSQL port 55493, game endpoints shifted by -13000.");
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
            ConnectionString = Connection,
            DatabaseEngine = DatabaseEngine.Npgsql,
        };
    }
}
