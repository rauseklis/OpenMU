// <copyright file="ChaosItemUpgradeRatesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using B = MUnique.OpenMU.Persistence.BasicModel;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>Checks actual upgrade handlers and one-time installation into both worlds' shared configuration.</summary>
[TestFixture]
[NonParallelizable]
public class ChaosItemUpgradeRatesTests
{
    private static readonly byte[] MixNumbers = [3, 4, 22, 23, 49, 50];
    private readonly InMemoryPersistenceContextProvider _provider = new();
    private GameConfiguration _configuration = null!;

    /// <summary>Creates a disposable in-memory Season Six configuration, without real database access.</summary>
    [OneTimeSetUp]
    public async Task InitializeAsync()
    {
        await new SeasonSixDataInitialization(this._provider, NullLoggerFactory.Instance).CreateInitialDataAsync(2, false);
        using var context = this._provider.CreateNewContext();
        this._configuration = (await context.GetAsync<GameConfiguration>()).Single();
    }

    /// <summary>The approved image's complete normal/Excellent/Ancient tables are exercised, plus overlapping penalties.</summary>
    private static IEnumerable<TestCaseData> UpgradeCases()
    {
        int[] bases = [60, 60, 55, 55, 20, 20];
        for (var index = 0; index < bases.Length; index++)
        {
            foreach (var kind in new[] { "Normal", "Excellent", "Ancient", "Guardian", "Socket", "ExcellentSocket", "ExcellentGuardianSocket" })
            {
                foreach (var luck in new[] { false, true })
                {
                    var penalty = (kind.Contains("Excellent", StringComparison.Ordinal) || kind == "Ancient" ? 10 : 0)
                        + (kind.Contains("Guardian", StringComparison.Ordinal) ? 10 : 0)
                        + (kind.Contains("Socket", StringComparison.Ordinal) ? 20 : 0);
                    yield return new TestCaseData(index, kind, luck, Math.Max(0, bases[index] + (luck ? 20 : 0) - penalty));
                }
            }
        }
    }

    /// <summary>Uses the real matching/rate handler, including the zero floor for late socket combinations.</summary>
    /// <param name="index">The recipe index.</param>
    /// <param name="kind">The equipment categories.</param>
    /// <param name="luck">Whether Luck is present.</param>
    /// <param name="expected">The expected effective percentage.</param>
    [TestCaseSource(nameof(UpgradeCases))]
    public async Task ActualHandlerUsesApprovedRates(int index, string kind, bool luck, int expected)
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var settings = this.UpgradeSettings(index);
        var definition = new B.ItemDefinition { Id = Guid.NewGuid(), Group = 7, Number = 0, Width = 1, Height = 1, Durability = 100, ItemSlot = new B.ItemSlotType() };
        var item = new B.Item { Id = Guid.NewGuid(), Definition = definition, Level = (byte)(9 + index), Durability = 100 };
        if (kind.Contains("Excellent", StringComparison.Ordinal))
        {
            item.ItemOptions.Add(CreateOption(ItemOptionTypes.Excellent));
        }

        if (kind == "Ancient")
        {
            item.ItemOptions.Add(CreateOption(ItemOptionTypes.AncientBonus));
            item.ItemSetGroups.Add(new B.ItemOfItemSet { AncientSetDiscriminator = 1 });
        }

        if (kind.Contains("Guardian", StringComparison.Ordinal))
        {
            var guardianOptions = new B.ItemOptionDefinition();
            guardianOptions.PossibleOptions.Add(new B.IncreasableItemOption { OptionType = ItemOptionTypes.GuardianOption });
            definition.PossibleItemOptions.Add(guardianOptions);
        }

        if (kind.Contains("Socket", StringComparison.Ordinal))
        {
            definition.MaximumSockets = 5;
            item.SocketCount = 1;
        }

        if (luck)
        {
            item.ItemOptions.Add(CreateOption(ItemOptionTypes.Luck));
        }

        await player.TemporaryStorage!.AddItemAsync(item);
        foreach (var required in settings.RequiredItems.Where(required => required.Reference == 0))
        {
            for (var count = 0; count < required.MinimumAmount; count++)
            {
                await player.TemporaryStorage.AddItemAsync(new B.Item { Id = Guid.NewGuid(), Definition = required.PossibleItems.Single(), Durability = 1 });
            }
        }

        var result = new SimpleItemCraftingHandler(settings).TryGetRequiredItems(player, out _, out var rate);
        Assert.That(result, Is.Null);
        Assert.That(rate, Is.EqualTo(expected));
    }

    /// <summary>Existing installations receive only the six intended recipe changes, exactly once.</summary>
    [Test]
    public async Task InstallerUpdatesExistingConfigurationOnceAndPreservesOtherSettings()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(2, false);
        using var context = provider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>()).Single();
        var goblin = configuration.Monsters.Single(monster => monster.Number == 238);
        var recipes = MixNumbers.Select(number => goblin.ItemCraftings.Single(crafting => crafting.Number == number)).ToArray();
        var retained = recipes.Select(crafting => (crafting.SimpleCraftingSettings!.Money, crafting.SimpleCraftingSettings.SuccessPercentageAdditionForGuardianItem,
            crafting.SimpleCraftingSettings.SuccessPercentageAdditionForSocketItem, crafting.SimpleCraftingSettings.RequiredItems.ToArray(), crafting.SimpleCraftingSettings.ResultItems.ToArray())).ToArray();
        var unrelated = goblin.ItemCraftings.Where(crafting => !MixNumbers.Contains(crafting.Number)).Select(crafting => (crafting, Snapshot(crafting.SimpleCraftingSettings))).ToArray();
        var worlds = (await context.GetAsync<GameServerDefinition>()).ToArray();
        var experience = worlds.Select(world => world.ExperienceRate).ToArray();
        foreach (var recipe in recipes)
        {
            recipe.SimpleCraftingSettings!.SuccessPercent = 99;
            recipe.SimpleCraftingSettings.SuccessPercentageAdditionForLuck = 25;
        }

        foreach (var update in (await context.GetAsync<ConfigurationUpdate>()).Where(update => update.Version == (int)UpdateVersion.ChaosItemUpgradeRates).ToArray())
        {
            await context.DeleteAsync(update);
        }

        await context.SaveChangesAsync();
        Assert.That(await ChaosItemUpgradeRatesInstaller.ApplyIfNeededAsync(provider), Is.True);
        int[] expectedBases = [60, 60, 55, 55, 20, 20];
        for (var index = 0; index < recipes.Length; index++)
        {
            var settings = recipes[index].SimpleCraftingSettings!;
            Assert.That(settings.SuccessPercent, Is.EqualTo(expectedBases[index]));
            Assert.That(settings.SuccessPercentageAdditionForLuck, Is.EqualTo(20));
            Assert.That(settings.Money, Is.EqualTo(retained[index].Money));
            Assert.That(settings.SuccessPercentageAdditionForGuardianItem, Is.EqualTo(retained[index].SuccessPercentageAdditionForGuardianItem));
            Assert.That(settings.SuccessPercentageAdditionForSocketItem, Is.EqualTo(retained[index].SuccessPercentageAdditionForSocketItem));
            Assert.That(settings.RequiredItems, Is.EqualTo(retained[index].Item4));
            Assert.That(settings.ResultItems, Is.EqualTo(retained[index].Item5));
        }

        foreach (var (crafting, snapshot) in unrelated)
        {
            Assert.That(Snapshot(crafting.SimpleCraftingSettings), Is.EqualTo(snapshot), crafting.Name.ToString());
        }

        Assert.That(worlds.Length, Is.EqualTo(2));
        Assert.That(worlds.Select(world => world.ExperienceRate), Is.EqualTo(experience));
        recipes[0].SimpleCraftingSettings!.SuccessPercent = 61;
        await context.SaveChangesAsync();
        Assert.That(await ChaosItemUpgradeRatesInstaller.ApplyIfNeededAsync(provider), Is.False);
        Assert.That(recipes[0].SimpleCraftingSettings!.SuccessPercent, Is.EqualTo(61));
        Assert.That((await context.GetAsync<ConfigurationUpdate>()).Count(update => update.Version == (int)UpdateVersion.ChaosItemUpgradeRates), Is.EqualTo(1));
    }

    private static B.ItemOptionLink CreateOption(ItemOptionType type)
        => new() { ItemOption = new B.IncreasableItemOption { OptionType = type }, Level = 1 };

    private static string Snapshot(SimpleCraftingSettings? settings)
        => settings is null ? "none" : string.Join(";", typeof(SimpleCraftingSettings).GetProperties()
            .Where(property => property.PropertyType.IsValueType).OrderBy(property => property.Name).Select(property => $"{property.Name}={property.GetValue(settings)}"));

    private SimpleCraftingSettings UpgradeSettings(int index)
        => this._configuration.Monsters.Single(monster => monster.Number == 238).ItemCraftings.Single(crafting => crafting.Number == MixNumbers[index]).SimpleCraftingSettings!;
}
