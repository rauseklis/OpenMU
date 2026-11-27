// <copyright file="DarkMageTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the Dark Mage (1113): a dark skeleton with a Skull Staff whose attacks are shown as Flame, 10 000 health,
/// everything else 100, attack range 4, a hit every 2 seconds, one in Devias at 223/83 (respawn 10 s), and one box per
/// kill: Silver Medal, Gold Medal, Box of Heaven, Firecracker or Heart of Love, 20% each.
/// </summary>
[TestFixture]
internal class DarkMageTest
{
    private const short DarkMage = 1113;
    private const byte DeviasNumber = 2;
    private const short Flame = 5;
    private const byte BoxGroup = 14;
    private const short BoxOfLuck = 11;

    // Box of Luck levels: Firecracker +2, Heart of Love +3, Silver Medal +5, Gold Medal +6, Box of Heaven +7.
    private static readonly byte[] DropLevels = [2, 3, 5, 6, 7];

    /// <summary>
    /// Tests that a new database contains the Dark Mage, its spawn and its drops.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsTheDarkMageAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var configuration = await CreateConfigurationAsync(provider).ConfigureAwait(false);

        AssertDarkMage(configuration);
    }

    /// <summary>
    /// Tests that the update adds the Dark Mage, its spawn and its drops to a database without it (like the live
    /// server), and that applying it twice adds nothing more.
    /// </summary>
    [Test]
    public async Task UpdateAddsTheDarkMageOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var configuration = await CreateConfigurationAsync(provider).ConfigureAwait(false);
        RemoveDarkMage(configuration);
        Assert.That(configuration.Monsters.Any(m => m.Number == DarkMage), Is.False);

        var update = new AddDarkMageUpdatePlugIn();
        await update.ApplyUpdateAsync(provider.CreateNewContext(), configuration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(provider.CreateNewContext(), configuration).ConfigureAwait(false);

        AssertDarkMage(configuration);
        Assert.That(update.IsMandatory, Is.True);
    }

    /// <summary>
    /// Tests that the installer adds the Dark Mage once and does nothing on a new database.
    /// </summary>
    [Test]
    public async Task InstallerAddsTheDarkMageOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        Assert.That(await DarkMageInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "new database");

        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var devias = configuration.Maps.Single(m => m.Number == DeviasNumber);
            foreach (var spawn in devias.MonsterSpawns.Where(s => s.MonsterDefinition?.Number == DarkMage).ToList())
            {
                devias.MonsterSpawns.Remove(spawn);
                await context.DeleteAsync(spawn).ConfigureAwait(false);
            }

            var monster = configuration.Monsters.Single(m => m.Number == DarkMage);
            foreach (var group in monster.DropItemGroups.ToList())
            {
                monster.DropItemGroups.Remove(group);
                configuration.DropItemGroups.Remove(group);
                await context.DeleteAsync(group).ConfigureAwait(false);
            }

            foreach (var attribute in monster.Attributes.ToList())
            {
                await context.DeleteAsync(attribute).ConfigureAwait(false);
            }

            configuration.Monsters.Remove(monster);
            await context.DeleteAsync(monster).ConfigureAwait(false);

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddDarkMage).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await DarkMageInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True, "first start");
        Assert.That(await DarkMageInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "second start");

        using var reloaded = provider.CreateNewContext();
        AssertDarkMage((await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single());
    }

    private static void RemoveDarkMage(GameConfiguration configuration)
    {
        var devias = configuration.Maps.Single(m => m.Number == DeviasNumber);
        foreach (var spawn in devias.MonsterSpawns.Where(s => s.MonsterDefinition?.Number == DarkMage).ToList())
        {
            devias.MonsterSpawns.Remove(spawn);
        }

        var monster = configuration.Monsters.Single(m => m.Number == DarkMage);
        foreach (var group in monster.DropItemGroups.ToList())
        {
            monster.DropItemGroups.Remove(group);
            configuration.DropItemGroups.Remove(group);
        }

        configuration.Monsters.Remove(monster);
    }

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider provider)
    {
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    private static void AssertDarkMage(GameConfiguration configuration)
    {
        var mage = configuration.Monsters.Single(m => m.Number == DarkMage);
        Assert.That(mage.Designation.ToString(), Is.EqualTo("Dark Mage"));
        Assert.That(mage.AttackSkill?.Number, Is.EqualTo(Flame));
        Assert.That(mage.RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(10)));
        Assert.That(mage.AttackRange, Is.EqualTo(4));
        Assert.That(mage.AttackDelay, Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(mage[Stats.Level], Is.EqualTo(100));
        Assert.That(mage[Stats.MaximumHealth], Is.EqualTo(10000));
        Assert.That(mage[Stats.MinimumPhysBaseDmg], Is.EqualTo(100));
        Assert.That(mage[Stats.MaximumPhysBaseDmg], Is.EqualTo(100));
        Assert.That(mage[Stats.DefenseBase], Is.EqualTo(100));
        Assert.That(mage[Stats.AttackRatePvm], Is.EqualTo(100));
        Assert.That(mage[Stats.DefenseRatePvm], Is.EqualTo(100));

        var spawns = configuration.Maps.Single(m => m.Number == DeviasNumber).MonsterSpawns.Where(s => s.MonsterDefinition?.Number == DarkMage).ToList();
        Assert.That(spawns, Has.Count.EqualTo(1));
        Assert.That((spawns[0].X1, spawns[0].X2, spawns[0].Y1, spawns[0].Y2, spawns[0].Quantity), Is.EqualTo(((byte)223, (byte)223, (byte)83, (byte)83, (short)1)));

        Assert.That(mage.NumberOfMaximumItemDrops, Is.EqualTo(1));
        Assert.That(DefaultDropGenerator.HasCompleteOwnDropTable(mage), Is.True);
        Assert.That(mage.DropItemGroups, Has.Count.EqualTo(5));
        Assert.That(mage.DropItemGroups.All(g => g.Chance == 0.2 && g.PossibleItems.Single() is { Group: BoxGroup, Number: BoxOfLuck }), Is.True);
        Assert.That(mage.DropItemGroups.Select(g => g.ItemLevel).OrderBy(l => l), Is.EqualTo(DropLevels));
        Assert.That(mage.DropItemGroups.All(g => g.Monster == mage && configuration.DropItemGroups.Contains(g)), Is.True);
    }
}
