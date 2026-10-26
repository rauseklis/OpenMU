// <copyright file="ChocolateEndgameDatabaseTests.cs" company="MUnique">
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
/// Upgrades a clone of the task-owned old fixture, including a persisted legacy socket item.
/// </summary>
[TestFixture]
public class ChocolateEndgameDatabaseTests
{
    private const string DatabaseName = "mu_chocolate_endgame_20261026_verified";
    private const string Connection = "Host=127.0.0.1;Port=55503;Database=" + DatabaseName + ";User Id=progressionvalidation;Command Timeout=120;";

    [Test]
    [Explicit("Requires CHOCOLATE_DATABASE_FIXTURE=clone-owned and the task-owned loopback 55503 cluster.")]
    public async Task UpgradeLegacySocketItemAndReloadAsync()
    {
        Assert.That(Environment.GetEnvironmentVariable("CHOCOLATE_DATABASE_FIXTURE"), Is.EqualTo("clone-owned"));
        await using (var admin = new NpgsqlConnection(Connection.Replace(DatabaseName, "postgres", StringComparison.Ordinal)))
        {
            await admin.OpenAsync().ConfigureAwait(false);
            await using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin);
            exists.Parameters.AddWithValue("name", DatabaseName);
            Assert.That(Convert.ToInt64(await exists.ExecuteScalarAsync().ConfigureAwait(false)), Is.Zero, "Refusing to overwrite an existing fixture.");
            await using var create = new NpgsqlCommand("CREATE DATABASE " + DatabaseName + " TEMPLATE mu_bonus_progression_20261026 OWNER progressionvalidation", admin);
            await create.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        ConnectionConfigurator.Initialize(new FixtureConnections());
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        Guid legacyId;
        Guid[] ordinaryLinks;
        string legacyProperties;
        string otherBoxes;
        int itemCount;
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            otherBoxes = OtherBoxFingerprint(configuration);
            var legacy = context.CreateNew<Item>();
            legacy.Definition = configuration.Items.Single(item => item.Group == 6 && item.Number == 20);
            legacy.Level = 9;
            legacy.Durability = 23;
            legacy.StorePrice = 12345;
            legacy.ItemSlot = 110;
            legacy.SocketCount = 3;
            configuration.Monsters.Single(monster => monster.Number == 255).MerchantStore!.Items.Add(legacy);
            foreach (var type in new[] { ItemOptionTypes.SocketOption, ItemOptionTypes.SocketBonusOption, ItemOptionTypes.Luck, ItemOptionTypes.Option })
            {
                var option = configuration.ItemOptions.SelectMany(options => options.PossibleOptions).First(option => option.OptionType == type);
                var link = context.CreateNew<ItemOptionLink>();
                link.ItemOption = option;
                link.Level = 1;
                legacy.ItemOptions.Add(link);
            }

            legacyId = legacy.GetId();
            ordinaryLinks = legacy.ItemOptions.Where(link => link.ItemOption!.OptionType == ItemOptionTypes.Luck || link.ItemOption.OptionType == ItemOptionTypes.Option).Select(link => link.GetId()).ToArray();
            legacyProperties = ItemFingerprint(legacy);
            await context.SaveChangesAsync().ConfigureAwait(false);
            itemCount = (await context.GetAsync<Item>().ConfigureAwait(false)).Count();
        }

        Assert.That(await ChocolateEndgameInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await ChocolateEndgameInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using var reload = provider.CreateNewContext();
        var after = (await reload.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var items = (await reload.GetAsync<Item>().ConfigureAwait(false)).ToArray();
        var reloaded = items.Single(item => item.GetId() == legacyId);
        Assert.That(items.Length, Is.EqualTo(itemCount), "No item may be deleted by socket cleanup.");
        Assert.That(ItemFingerprint(reloaded), Is.EqualTo(legacyProperties));
        Assert.That(reloaded.SocketCount, Is.Zero);
        Assert.That(reloaded.ItemOptions.Select(link => link.GetId()), Is.EquivalentTo(ordinaryLinks));
        Assert.That(after.Items.All(item => item.MaximumSockets == 0), Is.True);
        Assert.That(OtherBoxFingerprint(after), Is.EqualTo(otherBoxes), "Star and all unrelated opening pools must remain unchanged.");
        Assert.That(after.Items.Single(item => item.Group == 14 && item.Number == 32).DropItems.Count(group => group.SourceItemLevel == 0), Is.EqualTo(20));
        Assert.That(after.Items.Single(item => item.Group == 14 && item.Number == 34).DropItems.Count(group => group.SourceItemLevel == 0), Is.EqualTo(13));
        Assert.That(after.Items.Single(item => item.Group == 14 && item.Number == 33).DropItems.Single().PossibleItems.Single().Number, Is.EqualTo(23));
        await using var database = new EntityDataContext();
        Assert.That(database.Database.HasPendingModelChanges(), Is.False);
        TestContext.WriteLine("Persisted legacy socket item retained its ID, definition, level, durability, slot and price; ordinary Luck/ADD links retained, socket count and two socket links removed.");
    }

    private static string ItemFingerprint(Item item) => $"{item.GetId()}:{item.Definition!.GetId()}:{item.Level}:{item.Durability}:{item.ItemSlot}:{item.StorePrice}:{item.HasSkill}:{item.PetExperience}";

    private static string OtherBoxFingerprint(GameConfiguration configuration) => string.Join('\n', configuration.Items.Where(item => !(item.Group == 14 && item.Number is 32 or 33 or 34))
        .OrderBy(item => item.Group).ThenBy(item => item.Number).SelectMany(item => item.DropItems.OrderBy(group => group.GetId())
            .Select(group => $"{group.GetId()}:{group.SourceItemLevel}:{group.Chance}:{group.ItemType}:{group.MinimumLevel}:{group.MaximumLevel}:{group.MoneyAmount}:{string.Join(',', group.PossibleItems.Select(candidate => candidate.GetId()).Order())}")));

    private sealed class FixtureConnections : IDatabaseConnectionSettingProvider
    {
        public Task? Initialization => null;
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ConnectionSetting GetConnectionSetting<TContextType>() where TContextType : DbContext => this.GetConnectionSetting(typeof(TContextType));
        public ConnectionSetting GetConnectionSetting(Type contextType) => new() { ContextTypeName = contextType.FullName, ConnectionString = Connection, DatabaseEngine = DatabaseEngine.Npgsql };
    }
}
