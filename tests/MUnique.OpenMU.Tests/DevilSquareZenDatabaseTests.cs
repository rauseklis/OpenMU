// <copyright file="DevilSquareZenDatabaseTests.cs" company="MUnique">
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
public class DevilSquareZenDatabaseTests
{
    private static readonly string FixtureDatabase = $"mu_ds_zen_100x_{Guid.NewGuid():N}";
    private static readonly string FixtureConnection = "Host=127.0.0.1;Port=55483;Database=" + FixtureDatabase + ";User Id=ribbonvalidation;Command Timeout=120;";

    /// <summary>
    /// Seeds a disposable database and verifies the one-time completion reward upgrade.
    /// </summary>
    [Test]
    [Explicit("Requires DS_ZEN_FIXTURE=seed-disposable and the task-owned cluster at 127.0.0.1:55483.")]
    public async Task UpgradePersistsListingsAndRunsOnceAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("DS_ZEN_FIXTURE"), Is.EqualTo("seed-disposable"));
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
        Dictionary<(byte Level, int? Rank), int> expectedZen;
        Dictionary<(byte Level, int? Rank), int> expectedExperience;
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var games = configuration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.DevilSquare).ToArray();
            expectedZen = games.SelectMany(game => game.Rewards.Where(reward => reward.RewardType == MiniGameRewardType.Money)
                .Select(reward => (game.GameLevel, reward.Rank, reward.RewardAmount)))
                .ToDictionary(entry => (entry.GameLevel, entry.Rank), entry => entry.RewardAmount);
            expectedExperience = games.SelectMany(game => game.Rewards.Where(reward => reward.RewardType == MiniGameRewardType.Experience)
                .Select(reward => (game.GameLevel, reward.Rank, reward.RewardAmount)))
                .ToDictionary(entry => (entry.GameLevel, entry.Rank), entry => entry.RewardAmount);
            foreach (var reward in games.SelectMany(game => game.Rewards).Where(reward => reward.RewardType == MiniGameRewardType.Money))
            {
                reward.RewardAmount /= 50;
            }

            foreach (var reward in games.SelectMany(game => game.Rewards).Where(reward => reward.RewardType == MiniGameRewardType.Experience))
            {
                reward.RewardAmount = 1;
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(update => update.Version == (int)UpdateVersion.DevilSquareCompletionRewards).ToArray())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await DevilSquareRewardsUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await DevilSquareRewardsUpdateInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using var reloaded = provider.CreateNewContext();
        var persisted = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        foreach (var game in persisted.MiniGameDefinitions.Where(game => game.Type == MiniGameType.DevilSquare))
        {
            var items = game.Rewards.Where(reward => reward.RewardType is MiniGameRewardType.Item or MiniGameRewardType.ItemDrop).ToArray();
            if (game.GameLevel == 7)
            {
                Assert.That(items, Is.Empty);
            }
            else
            {
                var castle = persisted.MiniGameDefinitions.Single(other => other.Type == MiniGameType.BloodCastle && other.GameLevel == game.GameLevel);
                var castleItems = castle.Rewards.Single(reward => reward.RewardType == MiniGameRewardType.Item);
                Assert.That(items, Has.Length.EqualTo(1));
                Assert.That(items[0].Rank, Is.EqualTo(1));
                Assert.That(items[0].RewardType, Is.EqualTo(MiniGameRewardType.Item));
                Assert.That(items[0].RewardAmount, Is.EqualTo(castleItems.RewardAmount));
                Assert.That(items[0].ItemReward, Is.SameAs(castleItems.ItemReward));
            }

            foreach (var reward in game.Rewards.Where(reward => reward.RewardType is MiniGameRewardType.Money or MiniGameRewardType.Experience))
            {
                var expected = reward.RewardType == MiniGameRewardType.Money ? expectedZen : expectedExperience;
                Assert.That(reward.RewardAmount, Is.EqualTo(expected[(game.GameLevel, reward.Rank)]));
            }
        }

        await using var database = new EntityDataContext();
        Assert.That(database.Database.HasPendingModelChanges(), Is.False);
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
