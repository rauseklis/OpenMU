// <copyright file="AbsoluteBonusProgressionDatabaseTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.PlugIns;
using Npgsql;

/// <summary>
/// Upgrades absolute bonus chances in a new clone of the owned current reward fixture.
/// </summary>
[TestFixture]
public class AbsoluteBonusProgressionDatabaseTests
{
    private const string DatabaseName = "mu_absolute_bonus_20261026";
    private const string Connection = "Host=127.0.0.1;Port=55503;Database=" + DatabaseName + ";User Id=progressionvalidation;Command Timeout=120;";

    [Test]
    [Explicit("Requires ABSOLUTE_BONUS_DATABASE_FIXTURE=clone-owned and the task-owned loopback 55503 cluster.")]
    public async Task UpgradeAbsoluteChancesAndReloadAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("ABSOLUTE_BONUS_DATABASE_FIXTURE"), Is.EqualTo("clone-owned"));
        await using (var admin = new NpgsqlConnection(Connection.Replace(DatabaseName, "postgres", StringComparison.Ordinal)))
        {
            await admin.OpenAsync().ConfigureAwait(false);
            await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin);
            exists.Parameters.AddWithValue("name", DatabaseName);
            Assert.That(Convert.ToInt64(await exists.ExecuteScalarAsync().ConfigureAwait(false)), Is.Zero, "Refusing to overwrite an existing fixture.");
            await using var create = new NpgsqlCommand("CREATE DATABASE " + DatabaseName + " TEMPLATE mu_cherry_revision_20261026 OWNER progressionvalidation", admin);
            await create.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        ConnectionConfigurator.Initialize(new FixtureConnections());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        string otherBoxes;
        int itemCount;
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            otherBoxes = OtherBoxFingerprint(configuration);
            itemCount = (await context.GetAsync<Item>().ConfigureAwait(false)).Count();
        }

        Assert.That(await AbsoluteBonusProgressionInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await AbsoluteBonusProgressionInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using var reload = provider.CreateNewContext();
        var after = (await reload.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var items = (await reload.GetAsync<Item>().ConfigureAwait(false)).ToArray();
        Assert.That(items.Length, Is.EqualTo(itemCount), "No existing item is changed by a reward-pool update.");
        Assert.That(OtherBoxFingerprint(after), Is.EqualTo(otherBoxes), "Every non-Heaven opening pool must remain unchanged.");
        var wrapper = after.Items.Single(item => item.Group == 14 && item.Number == 11);
        for (var tier = 1; tier <= 5; tier++)
        {
            var groups = wrapper.DropItems.Where(group => group.SourceItemLevel == tier + 7).ToArray();
            Assert.That(groups.Sum(group => group.Chance), Is.EqualTo(1).Within(1e-12));
            Assert.That(groups.All(group => group.ExcellentSecondOptionChance == (6 - tier) / 10.0), Is.True);
            Assert.That(groups.All(group => group.ItemType == SpecialItemType.Excellent && group.MinimumLevel == 0 && group.MaximumLevel == 0), Is.True);
            Assert.That(groups.SelectMany(group => group.PossibleItems).All(item => item.MaximumSockets == 0), Is.True);
        }

        var heaven = wrapper.DropItems.Where(group => group.SourceItemLevel == 7).ToArray();
        Assert.That(heaven.Length, Is.EqualTo(4));
        Assert.That(heaven.Sum(group => group.Chance), Is.EqualTo(1).Within(1e-12));
        Assert.That(heaven.SelectMany(group => group.PossibleItems).Any(item => item.Group == 13 && item.Number == 15), Is.False);
        var servers = await reload.GetAsync<GameServerDefinition>().ConfigureAwait(false);
        Assert.That(servers.Single(server => server.ServerID == 0).SmallBonusDropChance, Is.Null);
        var bonuses = after.Maps.SelectMany(map => map.DropItemGroups).Where(group => group.ItemType == SpecialItemType.SmallBonus).ToArray();
        var chances = new Dictionary<byte, double> { [1] = .0005, [2] = .0015, [3] = .0035, [5] = .01, [6] = .005, [7] = .0025 };
        Assert.That(bonuses.All(group => Math.Abs(group.Chance - chances[group.ItemLevel!.Value]) < 1e-12), Is.True);
        Assert.That(bonuses.Where(group => group.ItemLevel == 5).All(group => group.MinimumMonsterLevel == 1 && group.MaximumMonsterLevel == 44), Is.True);
        await using var database = new EntityDataContext();
        Assert.That(database.Database.HasPendingModelChanges(), Is.False);
        TestContext.WriteLine("Absolute bonus upgrade persisted the chances, inclusive Silver limits and fruit-free Heaven; all non-Heaven pools/items retained and second installation skipped.");
    }

    private static string OtherBoxFingerprint(GameConfiguration configuration) => string.Join('\n', configuration.Items
        .OrderBy(item => item.Group).ThenBy(item => item.Number).SelectMany(item => item.DropItems.Where(group => !(item.Group == 14 && item.Number == 11 && group.SourceItemLevel == 7)).OrderBy(group => group.GetId())
            .Select(group => $"{group.GetId()}:{group.SourceItemLevel}:{group.Chance}:{group.ItemType}:{group.MinimumLevel}:{group.MaximumLevel}:{group.MoneyAmount}:{group.ExcellentSecondOptionChance}:{group.LuckChance}:{group.SkillChance}:{string.Join(',', group.PossibleItems.Select(candidate => candidate.GetId()).Order())}")));

    private sealed class FixtureConnections : IDatabaseConnectionSettingProvider
    {
        public Task? Initialization => null;
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ConnectionSetting GetConnectionSetting<TContextType>() where TContextType : DbContext => this.GetConnectionSetting(typeof(TContextType));
        public ConnectionSetting GetConnectionSetting(Type contextType) => new() { ContextTypeName = contextType.FullName, ConnectionString = Connection, DatabaseEngine = DatabaseEngine.Npgsql };
    }
}
