// <copyright file="DarkSkeletonDropsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the box drops of the dark skeletons: one box per kill, Box of Heaven (Box of Luck +7) 90%,
/// Pink (Dark Skeleton) or Blue (Dark Reaper) Chocolate Box 10%, nothing else.
/// </summary>
[TestFixture]
internal class DarkSkeletonDropsTest
{
    private const short DarkSkeleton = 1111;
    private const short DarkReaper = 1112;
    private const byte BoxGroup = 14;
    private const short BoxOfLuck = 11;
    private const short PinkChocolateBox = 32;
    private const short BlueChocolateBox = 34;

    /// <summary>
    /// Tests that a new database gives the dark skeletons their boxes.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsTheDropsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var configuration = await CreateConfigurationAsync(provider).ConfigureAwait(false);

        AssertDrops(configuration);
    }

    /// <summary>
    /// Tests that no other monster of a new database drops only from its own table, so the new drop rule
    /// changes nothing for them.
    /// </summary>
    [Test]
    public async Task OnlyTheDarkSkeletonsHaveACompleteOwnDropTableAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var configuration = await CreateConfigurationAsync(provider).ConfigureAwait(false);

        var numbers = configuration.Monsters
            .Where(m => m.ObjectKind != NpcObjectKind.Destructible && DefaultDropGenerator.HasCompleteOwnDropTable(m))
            .Select(m => m.Number)
            .OrderBy(n => n)
            .ToList();
        Assert.That(numbers, Is.EqualTo(new[] { DarkSkeleton, DarkReaper }));
    }

    /// <summary>
    /// Tests that the update adds the drops to existing monsters without drops, and that applying it twice
    /// doesn't add them twice.
    /// </summary>
    [Test]
    public async Task UpdateAddsTheDropsOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var configuration = await CreateConfigurationAsync(provider).ConfigureAwait(false);
        foreach (var monster in configuration.Monsters.Where(IsDarkSkeleton))
        {
            foreach (var group in monster.DropItemGroups.ToList())
            {
                monster.DropItemGroups.Remove(group);
                configuration.DropItemGroups.Remove(group);
            }
        }

        var update = new AddDarkSkeletonDropsUpdatePlugIn();
        await update.ApplyUpdateAsync(provider.CreateNewContext(), configuration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(provider.CreateNewContext(), configuration).ConfigureAwait(false);

        AssertDrops(configuration);
        Assert.That(update.IsMandatory, Is.True);
    }

    /// <summary>
    /// Tests that the installer adds the drops to a database which doesn't have them yet, once, and records it.
    /// </summary>
    [Test]
    public async Task InstallerAddsTheDropsOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            foreach (var monster in configuration.Monsters.Where(IsDarkSkeleton))
            {
                foreach (var group in monster.DropItemGroups.ToList())
                {
                    monster.DropItemGroups.Remove(group);
                    configuration.DropItemGroups.Remove(group);
                    await context.DeleteAsync(group).ConfigureAwait(false);
                }
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddDarkSkeletonDrops).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await DarkSkeletonDropsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True, "first start");
        Assert.That(await DarkSkeletonDropsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "second start");

        using var reloaded = provider.CreateNewContext();
        var reloadedConfiguration = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        AssertDrops(reloadedConfiguration);
        var installed = (await reloaded.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddDarkSkeletonDrops).ToList();
        Assert.That(installed, Has.Count.EqualTo(1));
        Assert.That(installed[0].InstalledAt, Is.Not.Null);
    }

    /// <summary>
    /// Tests that nothing is installed when the update is installed already, e.g. in a new database.
    /// </summary>
    [Test]
    public async Task InstallerDoesNothingOnANewDatabaseAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);

        Assert.That(await DarkSkeletonDropsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
    }

    private static bool IsDarkSkeleton(MonsterDefinition monster) => monster.Number is DarkSkeleton or DarkReaper;

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider provider)
    {
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    private static void AssertDrops(GameConfiguration configuration)
    {
        AssertDropsOf(configuration.Monsters.Single(m => m.Number == DarkSkeleton), PinkChocolateBox, configuration);
        AssertDropsOf(configuration.Monsters.Single(m => m.Number == DarkReaper), BlueChocolateBox, configuration);
    }

    private static void AssertDropsOf(MonsterDefinition monster, short chocolateBox, GameConfiguration configuration)
    {
        Assert.That(monster.NumberOfMaximumItemDrops, Is.EqualTo(1));
        Assert.That(monster.DropItemGroups, Has.Count.EqualTo(2));
        Assert.That(DefaultDropGenerator.HasCompleteOwnDropTable(monster), Is.True);

        var heaven = monster.DropItemGroups.Single(g => g.PossibleItems.Single().Number == BoxOfLuck);
        Assert.That(heaven.Chance, Is.EqualTo(0.9));
        Assert.That(heaven.ItemLevel, Is.EqualTo(7));
        Assert.That(heaven.PossibleItems.Single().Group, Is.EqualTo(BoxGroup));

        var chocolate = monster.DropItemGroups.Single(g => g.PossibleItems.Single().Number == chocolateBox);
        Assert.That(chocolate.Chance, Is.EqualTo(0.1));
        Assert.That(chocolate.ItemLevel, Is.EqualTo(0));
        Assert.That(chocolate.PossibleItems.Single().Group, Is.EqualTo(BoxGroup));

        Assert.That(monster.DropItemGroups.All(g => g.Monster == monster && configuration.DropItemGroups.Contains(g)), Is.True);
    }
}
