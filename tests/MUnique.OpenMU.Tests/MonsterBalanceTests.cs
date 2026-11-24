// <copyright file="MonsterBalanceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

/// <summary>
/// Tests the monster balance: own monster scaling for the x5 server, common drop chances halved, no excellent rings or pendants.
/// </summary>
[TestFixture]
public class MonsterBalanceTests
{
    /// <summary>
    /// Tests that a new database has the balanced values.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseIsBalancedAsync()
    {
        var (_, configuration) = await CreateAsync().ConfigureAwait(false);

        var spider = configuration.Monsters.Single(m => m.Number == 3);
        Assert.That(spider[Stats.MaximumHealth], Is.EqualTo(30), "Stored monster values are not changed.");
        Assert.That(spider[Stats.AttackRatePvm], Is.EqualTo(8), "Stored monster values are not changed.");

        var common = configuration.DropItemGroups.Where(g => MonsterBalance.IsCommonLootGroup(configuration, g)).ToList();
        Assert.That(common.Single(g => g.ItemType == SpecialItemType.Money).Chance, Is.EqualTo(0.25));
        Assert.That(common.Single(g => g.ItemType == SpecialItemType.RandomItem).Chance, Is.EqualTo(0.15));
        Assert.That(common.Single(g => g.ItemType == SpecialItemType.Excellent).Chance, Is.EqualTo(0.00005));

        var excellentGroups = configuration.DropItemGroups.Where(g => g.ItemType == SpecialItemType.Excellent);
        Assert.That(excellentGroups.SelectMany(g => g.PossibleItems).Any(ExcellentJewellery.IsExcellentRingOrPendant), Is.False);
    }

    /// <summary>
    /// Tests that the update balances an existing database once, and that special drop groups stay as they are.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task InstallerBalancesOnceAsync()
    {
        var (provider, configuration) = await CreateAsync().ConfigureAwait(false);
        var archangelChance = configuration.DropItemGroups.Single(g => g.Description.Value == "Scroll of Archangel+1").Chance;

        // Puts the database back into the state before the update.
        using (var context = provider.CreateNewContext())
        {
            var old = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var scalerConfiguration = old.PlugInConfigurations.Single(c => c.TypeId == typeof(MonsterAttributeScaler).GUID);
            scalerConfiguration.SetConfiguration(new MonsterAttributeScalerConfiguration { DamagePercentage = 0f, AttackRatePercentage = 150f, DefenseRatePercentage = 0f, DefensePercentage = 0f, HealthPercentage = 20f, OnlyServerId = 0 }, null);
            scalerConfiguration.IsActive = true;

            old.DropItemGroups.Single(g => g.ItemType == SpecialItemType.Money && g.Monster is null && old.Maps.Any(m => m.DropItemGroups.Contains(g))).Chance = 0.5;
            old.DropItemGroups.Single(g => g.ItemType == SpecialItemType.Excellent).PossibleItems.Add(old.Items.Single(i => i.Group == 13 && i.Number == 21));
            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.MonsterBalance).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await MonsterBalanceInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True, "first start");
        Assert.That(await MonsterBalanceInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "second start");

        using var reloaded = provider.CreateNewContext();
        var result = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var spider = result.Monsters.Single(m => m.Number == 3);
        Assert.That(spider[Stats.MaximumHealth], Is.EqualTo(30), "Stored monster values are not changed.");
        Assert.That(spider[Stats.AttackRatePvm], Is.EqualTo(8), "Stored monster values are not changed.");
        var scaler = AssertScalerPerServer(result);

        // The plug-in applies the right factor to each server and nothing to any other server.
        var plugIn = new MonsterAttributeScaler { Configuration = scaler };
        Assert.That(plugIn.GetFactors(0), Is.EqualTo((2.5f, 1.2f)), "x500: +150 % attack rate, +20 % health");
        Assert.That(plugIn.GetFactors(1), Is.EqualTo((1.5f, 1.2f)), "x5: +50 % attack rate, +20 % health");
        Assert.That(plugIn.GetFactors(2), Is.EqualTo((1.0f, 1.0f)), "other servers are not scaled");
        Assert.That(result.DropItemGroups.Single(g => g.ItemType == SpecialItemType.Money && g.Monster is null && result.Maps.Any(m => m.DropItemGroups.Contains(g))).Chance, Is.EqualTo(0.25));
        Assert.That(result.DropItemGroups.Single(g => g.Description.Value == "Scroll of Archangel+1").Chance, Is.EqualTo(archangelChance), "Special groups are not touched.");
        Assert.That(result.DropItemGroups.Single(g => g.ItemType == SpecialItemType.Excellent).PossibleItems.Any(ExcellentJewellery.IsExcellentRingOrPendant), Is.False);
        Assert.That(result.Items.Any(i => i.Group == 13 && i.Number == 21), Is.True, "The ring itself must still exist.");
    }

    /// <summary>
    /// Tests that monsters never drop an excellent ring or pendant from the random excellent pool, but still excellent items.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RandomExcellentDropsAreNeverRingsOrPendantsAsync()
    {
        var (_, configuration) = await CreateAsync().ConfigureAwait(false);
        Assert.That(configuration.Items.Count(ExcellentJewellery.IsExcellentRingOrPendant), Is.GreaterThanOrEqualTo(8));
        Assert.That(configuration.Items.Where(ExcellentJewellery.IsExcellentRingOrPendant).Any(i => i.DropsFromMonsters), Is.True, "The pool contains them, so the filter is what keeps them away.");

        var generator = new ExcellentProbe(configuration, new SeededRandomizer(7));
        var dropped = 0;
        for (var i = 0; i < 3000; i++)
        {
            var item = generator.Excellent(150);
            if (item is null)
            {
                continue;
            }

            dropped++;
            Assert.That(ExcellentJewellery.IsExcellentRingOrPendant(item.Definition!), Is.False, item.Definition!.Name);
        }

        Assert.That(dropped, Is.GreaterThan(1000));
    }

    private static MonsterAttributeScalerConfiguration AssertScalerPerServer(GameConfiguration configuration)
    {
        var plugInConfiguration = configuration.PlugInConfigurations.Single(c => c.TypeId == typeof(MonsterAttributeScaler).GUID);
        var scaler = plugInConfiguration.GetConfiguration<MonsterAttributeScalerConfiguration>(null)!;
        Assert.That(plugInConfiguration.IsActive, Is.True);
        Assert.That(scaler.OnlyServerId, Is.EqualTo((byte)0), "x500 stays as it was.");
        Assert.That(scaler.AttackRatePercentage, Is.EqualTo(150f));
        Assert.That(scaler.HealthPercentage, Is.EqualTo(20f));
        Assert.That(scaler.OtherServerId, Is.EqualTo((byte)1), "x5 has its own scaling.");
        Assert.That(scaler.OtherServerAttackRatePercentage, Is.EqualTo(50f));
        Assert.That(scaler.OtherServerHealthPercentage, Is.EqualTo(20f));
        Assert.That(scaler.DamagePercentage, Is.EqualTo(0f));
        Assert.That(scaler.DefensePercentage, Is.EqualTo(0f));
        Assert.That(scaler.DefenseRatePercentage, Is.EqualTo(0f));
        return scaler;
    }

    private static async Task<(InMemoryPersistenceContextProvider Provider, GameConfiguration Configuration)> CreateAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        return (provider, (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single());
    }
}

/// <summary>
/// Exposes the random excellent item generation of the drop generator.
/// </summary>
internal sealed class ExcellentProbe(GameConfiguration configuration, IRandomizer randomizer) : DefaultDropGenerator(configuration, randomizer)
{
    public Item? Excellent(int monsterLevel) => this.GenerateRandomExcellentItem(monsterLevel);
}
