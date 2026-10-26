// <copyright file="CherryExchangeRevisionDatabaseTests.cs" company="MUnique">
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
/// Upgrades only Cherry Blossom exchanges in a new clone of the owned Kundun fixture.
/// </summary>
[TestFixture]
public class CherryExchangeRevisionDatabaseTests
{
    private const string DatabaseName = "mu_cherry_revision_20261026";
    private const string Connection = "Host=127.0.0.1;Port=55503;Database=" + DatabaseName + ";User Id=progressionvalidation;Command Timeout=120;";

    [Test]
    [Explicit("Requires CHERRY_REVISION_DATABASE_FIXTURE=clone-owned and the task-owned loopback 55503 cluster.")]
    public async Task UpgradeOnlyExchangesAndReloadAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("CHERRY_REVISION_DATABASE_FIXTURE"), Is.EqualTo("clone-owned"));
        await using (var admin = new NpgsqlConnection(Connection.Replace(DatabaseName, "postgres", StringComparison.Ordinal)))
        {
            await admin.OpenAsync().ConfigureAwait(false);
            await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin);
            exists.Parameters.AddWithValue("name", DatabaseName);
            Assert.That(Convert.ToInt64(await exists.ExecuteScalarAsync().ConfigureAwait(false)), Is.Zero, "Refusing to overwrite an existing fixture.");
            await using var create = new NpgsqlCommand("CREATE DATABASE " + DatabaseName + " TEMPLATE mu_kundun_progression_20261026 OWNER progressionvalidation", admin);
            await create.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        ConnectionConfigurator.Initialize(new FixtureConnections());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        string otherBoxes;
        string whiteBefore;
        int itemCount;
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            otherBoxes = OtherBoxFingerprint(configuration);
            whiteBefore = WhiteFingerprint(configuration);
            itemCount = (await context.GetAsync<Item>().ConfigureAwait(false)).Count();
        }

        Assert.That(await CherryExchangeRevisionInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await CherryExchangeRevisionInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using var reload = provider.CreateNewContext();
        var after = (await reload.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var items = (await reload.GetAsync<Item>().ConfigureAwait(false)).ToArray();
        Assert.That(items.Length, Is.EqualTo(itemCount), "No existing item is changed by a reward-pool update.");
        Assert.That(OtherBoxFingerprint(after), Is.EqualTo(otherBoxes), "All opening pools, including Kundun, Chocolate, Star and Ancient, must remain unchanged.");
        Assert.That(WhiteFingerprint(after), Is.EqualTo(whiteBefore), "White rewards must not change.");
        var recipes = after.Monsters.Single(monster => monster.Number == 450).ItemCraftings;
        foreach (var recipe in recipes.Where(recipe => recipe.Number is 41 or 42 or 43))
        {
            var settings = recipe.SimpleCraftingSettings!;
            Assert.That(settings.ResultItems.Count, Is.EqualTo(6));
            Assert.That(settings.ResultItemSelect, Is.EqualTo(MUnique.OpenMU.DataModel.Configuration.ItemCrafting.ResultItemSelection.Any));
            Assert.That(settings.SuccessPercent, Is.EqualTo(100));
            Assert.That(settings.Money, Is.Zero);
        }

        var red = recipes.Single(recipe => recipe.Number == 43).SimpleCraftingSettings!.ResultItems;
        Assert.That(red.Single(result => result.ItemDefinition is { Group: 14, Number: 11 }).RandomMinimumLevel, Is.EqualTo(8));
        Assert.That(red.Single(result => result.ItemDefinition is { Group: 14, Number: 11 }).RandomMaximumLevel, Is.EqualTo(8));
        var golden = recipes.Single(recipe => recipe.Number == 41).SimpleCraftingSettings!.ResultItems;
        Assert.That(golden.Single(result => result.ItemDefinition is { Group: 14, Number: 11 }).RandomMinimumLevel, Is.EqualTo(10));
        Assert.That(golden.Single(result => result.ItemDefinition is { Group: 14, Number: 11 }).RandomMaximumLevel, Is.EqualTo(10));
        Assert.That(golden.Any(result => result.ItemDefinition is { Group: 14, Number: 123 }), Is.False);
        Assert.That(golden.Any(result => result.ItemDefinition is { Group: 14, Number: 124 }), Is.True);
        Assert.That(golden.Where(result => result.ItemDefinition is { Group: 12, Number: 30 or 31 }).All(result => result.RandomMinimumLevel == 0 && result.RandomMaximumLevel == 0), Is.True);
        await using var database = new EntityDataContext();
        Assert.That(database.Database.HasPendingModelChanges(), Is.False);
        TestContext.WriteLine("Exchange upgrade persisted six equal entries per tier, exact Kundun wrapper levels, preserved White/all box pools/items, and skipped its second installation.");
    }

    private static string WhiteFingerprint(GameConfiguration configuration)
    {
        var settings = configuration.Monsters.Single(monster => monster.Number == 450).ItemCraftings.Single(recipe => recipe.Number == 42).SimpleCraftingSettings!;
        return $"{settings.SuccessPercent}:{settings.Money}:{settings.ResultItemSelect}:" + string.Join(';', settings.ResultItems.Select(result => $"{result.ItemDefinition!.Group}/{result.ItemDefinition.Number}:{result.RandomMinimumLevel}:{result.RandomMaximumLevel}:{result.Durability}").Order());
    }

    private static string OtherBoxFingerprint(GameConfiguration configuration) => string.Join('\n', configuration.Items
        .OrderBy(item => item.Group).ThenBy(item => item.Number).SelectMany(item => item.DropItems.OrderBy(group => group.GetId())
            .Select(group => $"{group.GetId()}:{group.SourceItemLevel}:{group.Chance}:{group.ItemType}:{group.MinimumLevel}:{group.MaximumLevel}:{group.MoneyAmount}:{group.ExcellentSecondOptionChance}:{group.LuckChance}:{group.SkillChance}:{string.Join(',', group.PossibleItems.Select(candidate => candidate.GetId()).Order())}")));

    private sealed class FixtureConnections : IDatabaseConnectionSettingProvider
    {
        public Task? Initialization => null;
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ConnectionSetting GetConnectionSetting<TContextType>() where TContextType : DbContext => this.GetConnectionSetting(typeof(TContextType));
        public ConnectionSetting GetConnectionSetting(Type contextType) => new() { ContextTypeName = contextType.FullName, ConnectionString = Connection, DatabaseEngine = DatabaseEngine.Npgsql };
    }
}
