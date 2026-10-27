// <copyright file="UniqueRewardWeightsDatabaseTests.cs" company="MUnique">
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
/// Upgrades reward weights only in a new clone of the owned Cherry fixture.
/// </summary>
[TestFixture]
public class UniqueRewardWeightsDatabaseTests
{
    private const string DatabaseName = "mu_unique_rewards_20261027";
    private const string Connection = "Host=127.0.0.1;Port=55503;Database=" + DatabaseName + ";User Id=progressionvalidation;Command Timeout=120;";

    [Test]
    [Explicit("Requires UNIQUE_REWARD_DATABASE_FIXTURE=clone-owned and the task-owned loopback 55503 cluster.")]
    public async Task UpgradeOnlyWeightsAndReloadAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("UNIQUE_REWARD_DATABASE_FIXTURE"), Is.EqualTo("clone-owned"));
        await using (var admin = new NpgsqlConnection(Connection.Replace(DatabaseName, "postgres", StringComparison.Ordinal)))
        {
            await admin.OpenAsync().ConfigureAwait(false);
            await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin);
            exists.Parameters.AddWithValue("name", DatabaseName);
            Assert.That(Convert.ToInt64(await exists.ExecuteScalarAsync().ConfigureAwait(false)), Is.Zero, "Refusing to overwrite an existing fixture.");
            await using var create = new NpgsqlCommand("CREATE DATABASE " + DatabaseName + " TEMPLATE mu_cherry_weighted_20261027 OWNER progressionvalidation", admin);
            await create.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        ConnectionConfigurator.Initialize(new FixtureConnections());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        string otherBoxes;
        string whiteBefore;
        int itemCount;
        string eligibility;
        Dictionary<byte, double[]> totals;
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            otherBoxes = OtherBoxFingerprint(configuration);
            eligibility = Eligibility(configuration);
            totals = Totals(configuration);
            whiteBefore = WhiteFingerprint(configuration);
            itemCount = (await context.GetAsync<Item>().ConfigureAwait(false)).Count();
        }

        Assert.That(await UniqueRewardWeightsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await UniqueRewardWeightsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using var reload = provider.CreateNewContext();
        var after = (await reload.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var items = (await reload.GetAsync<Item>().ConfigureAwait(false)).ToArray();
        Assert.That(items.Length, Is.EqualTo(itemCount), "No existing item is removed by a reward-pool update.");
        Assert.That(OtherBoxFingerprint(after), Is.EqualTo(otherBoxes), "Opening pools outside the scoped equipment boxes remain unchanged.");
        Assert.That(WhiteFingerprint(after), Is.EqualTo(whiteBefore), "White rewards must not change.");
        Assert.That(Eligibility(after), Is.EqualTo(eligibility), "All equip restrictions stay unchanged.");
        foreach (var (level, oldTotals) in totals)
        {
            Assert.That(Totals(after)[level], Is.EqualTo(oldTotals).Within(1e-12));
        }

        var wrapper = after.Items.Single(item => item.Group == 14 && item.Number == 11);
        foreach (var (level, count) in new[] { (8, 6), (9, 7), (10, 7), (11, 7), (12, 9), (5, 6), (6, 6), (2, 7) })
        {
            var armor = wrapper.DropItems.Where(group => group.SourceItemLevel == level && group.PossibleItems.Count > 0 && group.PossibleItems.All(item => item.Group >= 7 && item.Group <= 11)).ToArray();
            Assert.That(armor.Length, Is.EqualTo(count));
            Assert.That(armor.Select(group => group.PossibleItems.First().Number).Distinct().Count(), Is.EqualTo(count));
            Assert.That(armor.All(group => Math.Abs(group.Chance - (armor.Sum(row => row.Chance) / count)) < 1e-12), Is.True);
        }

        foreach (var level in new[] { 1, 8, 9, 10, 11, 12 })
        {
            var itemsInRoutes = wrapper.DropItems.Where(group => group.SourceItemLevel == level).SelectMany(group => group.PossibleItems).Where(item => item.Group < 7).ToArray();
            Assert.That(itemsInRoutes.DistinctBy(item => (item.Group, item.Number)).Count(), Is.EqualTo(itemsInRoutes.Length));
        }

        await using var database = new EntityDataContext();
        Assert.That(database.Database.HasPendingModelChanges(), Is.False);
        TestContext.WriteLine("Weighted exchange upgrade persisted exact slots, exact Kundun wrapper levels, preserved White/all box pools/items, and skipped its second installation.");
    }

    private static string WhiteFingerprint(GameConfiguration configuration)
    {
        var settings = configuration.Monsters.Single(monster => monster.Number == 450).ItemCraftings.Single(recipe => recipe.Number == 42).SimpleCraftingSettings!;
        return $"{settings.SuccessPercent}:{settings.Money}:{settings.ResultItemSelect}:" + string.Join(';', settings.ResultItems.Select(result => $"{result.ItemDefinition!.Group}/{result.ItemDefinition.Number}:{result.RandomMinimumLevel}:{result.RandomMaximumLevel}:{result.Durability}").Order());
    }

    private static string OtherBoxFingerprint(GameConfiguration configuration) => string.Join('\n', configuration.Items
        .OrderBy(item => item.Group).ThenBy(item => item.Number).SelectMany(item => item.DropItems.Where(group => !(item.Group == 14 && item.Number == 11 && group.SourceItemLevel is 1 or 2 or 5 or 6 or >= 8 and <= 12)).OrderBy(group => group.GetId())
            .Select(group => $"{group.GetId()}:{group.SourceItemLevel}:{group.Chance}:{group.ItemType}:{group.MinimumLevel}:{group.MaximumLevel}:{group.MoneyAmount}:{group.ExcellentSecondOptionChance}:{group.LuckChance}:{group.SkillChance}:{string.Join(',', group.PossibleItems.Select(candidate => candidate.GetId()).Order())}")));

    private static string Eligibility(GameConfiguration configuration) => string.Join(';', configuration.Items.OrderBy(item => item.Group).ThenBy(item => item.Number)
        .Select(item => $"{item.Group}/{item.Number}:{item.MaximumSockets}:{string.Join(',', item.QualifiedCharacters.Select(character => character.Number).Order())}"));

    private static Dictionary<byte, double[]> Totals(GameConfiguration configuration) => configuration.Items.Single(item => item.Group == 14 && item.Number == 11).DropItems
        .Where(group => group.SourceItemLevel is 1 or 2 or 3 or 5 or 6 or >= 8 and <= 12).GroupBy(group => group.SourceItemLevel)
        .ToDictionary(groups => groups.Key, groups => new[] { groups.Sum(group => group.Chance),
            groups.Where(group => group.PossibleItems.Count > 0 && group.PossibleItems.All(item => item.Group is >= 7 and <= 11)).Sum(group => group.Chance),
            groups.Where(group => group.PossibleItems.Count > 0 && group.PossibleItems.All(item => item.Group < 6)).Sum(group => group.Chance),
            groups.Where(group => group.PossibleItems.Count > 0 && group.PossibleItems.All(item => item.Group == 6)).Sum(group => group.Chance) });

    private sealed class FixtureConnections : IDatabaseConnectionSettingProvider
    {
        public Task? Initialization => null;
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ConnectionSetting GetConnectionSetting<TContextType>() where TContextType : DbContext => this.GetConnectionSetting(typeof(TContextType));
        public ConnectionSetting GetConnectionSetting(Type contextType) => new() { ContextTypeName = contextType.FullName, ConnectionString = Connection, DatabaseEngine = DatabaseEngine.Npgsql };
    }
}
