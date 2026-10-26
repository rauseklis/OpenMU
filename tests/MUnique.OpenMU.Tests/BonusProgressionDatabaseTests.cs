// <copyright file="BonusProgressionDatabaseTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
/// Verifies migration, one-time upgrade and real item generation on a dedicated disposable database.
/// </summary>
[TestFixture]
public class BonusProgressionDatabaseTests
{
    private const string DatabaseName = "mu_bonus_progression_20261026";
    private const string Connection = "Host=127.0.0.1;Port=55503;Database=" + DatabaseName + ";User Id=progressionvalidation;Command Timeout=120;";

    [Test]
    [Explicit("Requires BONUS_PROGRESSION_FIXTURE=seed-disposable and the task-owned 127.0.0.1:55503 cluster.")]
    public async Task SeedUpgradeReloadAndGenerateAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("BONUS_PROGRESSION_FIXTURE"), Is.EqualTo("seed-disposable"));
        Assert.That(ConnectionConfigurator.IsInitialized, Is.False);
        await using (var connection = new NpgsqlConnection(Connection.Replace(DatabaseName, "postgres", StringComparison.Ordinal)))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", connection);
            command.Parameters.AddWithValue("name", DatabaseName);
            Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false)), Is.Zero, "Refusing to overwrite a fixture.");
        }

        ConnectionConfigurator.Initialize(new FixtureConnectionSettings());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        await provider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        string unchanged;
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            unchanged = UnchangedRewards(configuration);
            var wrapper = configuration.Items.Single(item => item.Group == 14 && item.Number == 11);
            foreach (var old in wrapper.DropItems.Where(group => group.SourceItemLevel == 1).ToArray())
            {
                wrapper.DropItems.Remove(old);
            }

            var legacy = context.CreateNew<ItemDropItemGroup>();
            legacy.SourceItemLevel = 1;
            legacy.ItemType = SpecialItemType.RandomItem;
            legacy.Chance = 1;
            legacy.PossibleItems.Add(configuration.Items.Single(item => item.Group == 0 && item.Number == 17));
            wrapper.DropItems.Add(legacy);
            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                .Where(update => update.Version == (int)UpdateVersion.BonusProgression).ToArray())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await BonusProgressionInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await BonusProgressionInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using (var reloaded = provider.CreateNewConfigurationContext())
        {
            var configuration = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            Assert.That(UnchangedRewards(configuration), Is.EqualTo(unchanged), "BC/DS, ribbons and every shop listing must remain unchanged.");
            var star = configuration.Items.Single(item => item.Group == 14 && item.Number == 11).DropItems.Where(group => group.SourceItemLevel == 1).ToList();
            Assert.That(star.Sum(group => group.Chance), Is.EqualTo(1).Within(1e-12));
            Assert.That(star.SelectMany(group => group.PossibleItems).Any(item => item.Group == 0 && item.Number == 17), Is.False);
            Assert.That(star.SelectMany(group => group.PossibleItems).Any(item => item.Group == 3 && item.Number == 10), Is.True);
            var generator = new DefaultDropGenerator(configuration, Rand.GetRandomizer());
            foreach (var group in star)
            {
                foreach (var definition in group.PossibleItems.ToArray())
                {
                    var candidates = group.PossibleItems.ToArray();
                    group.PossibleItems.Clear();
                    group.PossibleItems.Add(definition);
                    for (var draw = 0; draw < 20; draw++)
                    {
                        var item = generator.GenerateItemDrop(group);
                        Assert.That(item, Is.Not.Null);
                        Assert.That(item!.Definition, Is.EqualTo(definition));
                        Assert.That(item.Level, Is.InRange(group.MinimumLevel, group.MaximumLevel));
                        if (definition.Group == 6)
                        {
                            Assert.That(group.SuppressAdditionalOption, Is.True);
                            Assert.That(item.HasSkill, Is.False);
                            Assert.That(item.ItemOptions, Is.Empty);
                        }

                        if (definition.Group == 5 && definition.Number == 23)
                        {
                            Assert.That(item.HasSkill, Is.True);
                        }
                    }

                    group.PossibleItems.Clear();
                    foreach (var candidate in candidates)
                    {
                        group.PossibleItems.Add(candidate);
                    }
                }
            }
        }

        await using var database = new EntityDataContext();
        Assert.That(database.Database.HasPendingModelChanges(), Is.False);
        await database.Database.ExecuteSqlRawAsync("""
            UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-14000;
            UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-14000;
            UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-14000;
            """).ConfigureAwait(false);
    }

    private static string UnchangedRewards(GameConfiguration configuration)
    {
        var events = configuration.MiniGameDefinitions.OrderBy(game => game.GetId())
            .SelectMany(game => game.Rewards.OrderBy(reward => reward.GetId())
                .Select(reward => $"{game.GetId()}:{reward.GetId()}:{reward.RewardType}:{reward.RewardAmount}"));
        var ribbons = configuration.Items.Where(item => item.Group == 12 && item.Number is >= 32 and <= 34)
            .OrderBy(item => item.Number).SelectMany(item => item.DropItems.OrderBy(group => group.GetId())
                .Select(group => $"{group.GetId()}:{group.Chance}:{group.MoneyAmount}:{group.SkillChance}:{group.LuckChance}:{string.Join(',', group.PossibleItems.Select(reward => reward.GetId()).Order())}"));
        var stock = configuration.Monsters.Where(monster => monster.MerchantStore is not null).OrderBy(monster => monster.Number)
            .SelectMany(monster => monster.MerchantStore!.Items.OrderBy(item => item.GetId())
                .Select(item => $"{monster.Number}:{item.GetId()}:{item.Definition!.GetId()}:{item.ItemSlot}:{item.Level}:{item.Durability}:{item.StorePrice}"));
        return string.Join('\n', events.Concat(ribbons).Concat(stock));
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
