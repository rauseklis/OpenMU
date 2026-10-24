// <copyright file="BoxRewardDatabaseFixtureTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.PlugIns;
using Npgsql;

/// <summary>
/// Seeds only the task's disposable loopback cluster, without starting game listeners.
/// </summary>
[TestFixture]
public class BoxRewardDatabaseFixtureTests
{
    private static string DatabaseName => Environment.GetEnvironmentVariable("BOX_REWARD_FIXTURE_DATABASE") ?? "mu_box_rewards_20261024";

    private static string ConnectionString => $"Host=127.0.0.1;Port=55443;Database={DatabaseName};User Id=boxvalidation;";

    /// <summary>
    /// Upgrades a copy of the earlier disposable reward fixture and verifies reloaded Ancient rewards.
    /// </summary>
    [Test]
    [Explicit("Requires a disposable fixture copy and BOX_REWARD_FIXTURE=upgrade-ancient-disposable.")]
    public async Task UpgradeAncientBoxesAndReloadAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("BOX_REWARD_FIXTURE"), Is.EqualTo("upgrade-ancient-disposable"));
        Assert.That(Regex.IsMatch(DatabaseName, "^mu_box_rewards_ancient_[a-z0-9_]+$"), Is.True);
        Assert.That(ConnectionConfigurator.IsInitialized, Is.False);
        ConnectionConfigurator.Initialize(new FixtureConnectionSettings());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        await provider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);
        using (var context = provider.CreateNewContext())
        {
            var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var existingRewards = config.Items.Single(item => item.Group == 14 && item.Number == 11)
                .DropItems.Where(group => group.SourceItemLevel != 0).Select(group => group.GetId()).ToArray();
            var store = config.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
            var existingStock = store.Items.Select(item => (Id: item.GetId(), Slot: item.ItemSlot)).ToArray();
            Assert.That(await AncientTierBoxUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
            Assert.That(await AncientTierBoxUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
            using var verified = provider.CreateNewContext();
            var updated = (await verified.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var updatedStore = updated.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
            Assert.That(existingStock.All(previous => updatedStore.Items.Any(item => item.GetId() == previous.Id && item.ItemSlot == previous.Slot)), Is.True);
            Assert.That(updated.Items.Single(item => item.Group == 14 && item.Number == 11)
                .DropItems.Where(group => group.SourceItemLevel != 0).Select(group => group.GetId()), Is.EquivalentTo(existingRewards));
        }

        using var reloaded = provider.CreateNewConfigurationContext();
        var persisted = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var generator = new DefaultDropGenerator(persisted, Rand.GetRandomizer());
        foreach (var number in new short[] { 11, 124, 123 })
        {
            var store = persisted.Monsters.Single(monster => monster.Number == 255).MerchantStore!;
            var listing = store.Items.Single(item => item.Definition is { Group: 14 } definition && definition.Number == number && item.Level == 0);
            Assert.That(new ItemPriceCalculator().CalculateMerchantBuyingPrice(listing), Is.EqualTo(1000));
            var groups = persisted.Items.Single(item => item.Group == 14 && item.Number == number).DropItems.Where(group => group.SourceItemLevel == 0).ToList();
            Assert.That(groups.Sum(group => group.Chance), Is.EqualTo(1).Within(0.000000001));
            foreach (var group in groups)
            {
                Assert.That(group.AncientSet, Is.Not.Null);
                Assert.That(group.AncientBonusLevelTwoChance, Is.EqualTo(0.30));
                for (var draw = 0; draw < 100; draw++)
                {
                    var item = generator.GenerateItemDrop(group);
                    Assert.That(item, Is.Not.Null);
                    Assert.That(item!.Level, Is.Zero);
                    Assert.That(item.ItemSetGroups.Single().ItemSetGroup, Is.EqualTo(group.AncientSet));
                    Assert.That(group.PossibleItems, Does.Contain(item.Definition));
                }
            }
        }

        await using var database = new EntityDataContext();
        Assert.That(database.Database.HasPendingModelChanges(), Is.False);
    }

    /// <summary>
    /// Migrates and seeds a fresh fixture after explicit local opt-in.
    /// </summary>
    [Test]
    [Explicit("Requires the task-owned PostgreSQL cluster on loopback:55443 and BOX_REWARD_FIXTURE=seed-disposable.")]
    public async Task SeedDisposableDatabaseAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("BOX_REWARD_FIXTURE"), Is.EqualTo("seed-disposable"));
        Assert.That(Regex.IsMatch(DatabaseName, "^mu_box_rewards_[a-z0-9_]+$"), Is.True, "Only disposable fixture names are allowed.");
        Assert.That(ConnectionConfigurator.IsInitialized, Is.False, "Run this fixture alone in a fresh test process.");
        await using (var connection = new NpgsqlConnection("Host=127.0.0.1;Port=55443;Database=postgres;User Id=boxvalidation;"))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @name", connection);
            command.Parameters.AddWithValue("name", DatabaseName);
            Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false)), Is.Zero, "Refusing to overwrite an existing fixture.");
        }

        ConnectionConfigurator.Initialize(new FixtureConnectionSettings());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        await provider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        await using var database = new EntityDataContext();
        Assert.That(await database.Database.GetPendingMigrationsAsync().ConfigureAwait(false), Is.Empty);
        await database.Database.ExecuteSqlRawAsync("""
            UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-10000;
            UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-10000;
            UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-10000;
            """).ConfigureAwait(false);
    }

    /// <summary>
    /// Reloads the applied fixture through production persistence and opens each item reward.
    /// </summary>
    [Test]
    [Explicit("Requires the applied task-owned fixture and BOX_REWARD_FIXTURE=validate-disposable.")]
    public async Task AppliedRewardsLoadAndGenerateAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("BOX_REWARD_FIXTURE"), Is.EqualTo("validate-disposable"));
        Assert.That(Regex.IsMatch(DatabaseName, "^mu_box_rewards_[a-z0-9_]+$"), Is.True);
        Assert.That(ConnectionConfigurator.IsInitialized, Is.False);
        ConnectionConfigurator.Initialize(new FixtureConnectionSettings());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        using var context = provider.CreateNewConfigurationContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var server = (await context.GetAsync<GameServerDefinition>().ConfigureAwait(false)).Single(definition => definition.ServerID == 0);
        Assert.That(server.SmallBonusDropChance, Is.EqualTo(0.05));
        var generator = new DefaultDropGenerator(config, Rand.GetRandomizer(), server.SmallBonusDropsEnabled, server.SmallBonusDropChance);
        var box = config.Items.Single(item => item.Group == 14 && item.Number == 11);
        var merchant = config.Monsters.Single(monster => monster.Number == 255);
        Assert.That(merchant.MerchantStore, Is.Not.Null);
        var listings = merchant.MerchantStore!.Items.Where(item => item.Definition == box && item.StorePrice == 1000).ToList();
        Assert.That(listings.Select(item => item.Level), Is.EquivalentTo(new byte[] { 2, 3, 5, 6, 7, 8, 9, 10, 11, 12 }));
        var prices = new ItemPriceCalculator();
        Assert.That(listings.All(item => prices.CalculateMerchantBuyingPrice(item) == 1000), Is.True);
        var groups = box.DropItems.Where(group => group.Description.ToString()?.StartsWith("Approved boxes:", StringComparison.Ordinal) is true).ToList();
        Assert.That(groups.Count, Is.EqualTo(48));
        foreach (var group in groups.Where(group => group.ItemType != SpecialItemType.Money))
        {
            for (var draw = 0; draw < 100; draw++)
            {
                var item = generator.GenerateItemDrop(group);
                Assert.That(item, Is.Not.Null, group.Description.ToString());
                Assert.That(item!.Level, Is.InRange(group.MinimumLevel, group.MaximumLevel));
                Assert.That(group.PossibleItems, Does.Contain(item.Definition));
                if (group.GuaranteeJewelryOption)
                {
                    Assert.That(item.ItemOptions.Any(option => option.Level is >= 1 and <= 3), Is.True);
                }

                if (group.ItemType == SpecialItemType.Excellent)
                {
                    Assert.That(item.SocketCount, Is.Zero);
                    Assert.That(item.ItemOptions.Count(option => option.ItemOption?.OptionType == ItemOptionTypes.Excellent), Is.InRange(1, 2));
                    Assert.That(item.HasSkill, Is.EqualTo(item.CanHaveSkill()));
                }
            }
        }
    }

    private sealed class FixtureConnectionSettings : IDatabaseConnectionSettingProvider
    {
        public Task? Initialization => null;

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ConnectionSetting GetConnectionSetting<TContextType>()
            where TContextType : DbContext => this.GetConnectionSetting(typeof(TContextType));

        public ConnectionSetting GetConnectionSetting(Type contextType) => new()
        {
            ContextTypeName = contextType.FullName,
            ConnectionString = BoxRewardDatabaseFixtureTests.ConnectionString,
            DatabaseEngine = DatabaseEngine.Npgsql,
        };
    }
}
