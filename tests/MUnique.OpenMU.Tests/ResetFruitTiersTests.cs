// <copyright file="ResetFruitTiersTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests the update which sets the fruit costs of the resets (25, 50, 75, 100 and 150 per reset range).
/// </summary>
[TestFixture]
public class ResetFruitTiersTests
{
    // The reset configuration as it is stored on the live server (read-only copy, before the update).
    private const string LiveConfiguration = """
        {"$id": "", "LogOut": true, "MoveHome": true, "ResetLimit": 50, "ResetStats": true, "PointsTiers": {"$id": "", "$values": []}, "ItemCostTiers": {"$id": "", "$values": [{"$id": "", "MinimumResetCount": 6, "RequiredItemAmount": 100, "AdditionalItemAmountPerReset": 100}]}, "RequiredLevel": 400, "RequiredMoney": 1, "MoneyCostTiers": {"$id": "", "$values": [{"$id": "", "RequiredMoney": 25000000, "MinimumResetCount": 1, "MultiplyByResetCount": true}, {"$id": "", "RequiredMoney": 50000000, "MinimumResetCount": 6, "MultiplyByResetCount": false}]}, "PointsPerReset": 500, "LevelAfterReset": 10, "RequiredResetItem": null, "RequireNoEquipment": true, "ReplacePointsPerReset": true, "MultiplyPointsByResetCount": true, "PayItemCostsWithJewelBankFruits": true, "MultiplyRequiredMoneyByResetCount": true}
        """;

    /// <summary>
    /// Tests that only the item cost tiers change and everything else of the stored configuration stays as it is.
    /// </summary>
    [Test]
    public void OnlyTheItemCostTiersChange()
    {
        var changed = ResetFruitTiersUpdatePlugIn.ReplaceItemCostTiers(LiveConfiguration);

        Assert.That(changed, Is.Not.Null);
        var before = JsonNode.Parse(LiveConfiguration)!.AsObject();
        var after = JsonNode.Parse(changed!)!.AsObject();
        foreach (var property in before)
        {
            if (property.Key == "ItemCostTiers")
            {
                continue;
            }

            Assert.That(after[property.Key]?.ToJsonString(), Is.EqualTo(property.Value?.ToJsonString()), property.Key);
        }

        Assert.That(after.Count, Is.EqualTo(before.Count));
    }

    /// <summary>
    /// Tests the stored tiers and the fruits which the reset calculation asks for.
    /// </summary>
    /// <param name="resetNumber">The number of the reset.</param>
    /// <param name="expectedFruits">The expected fruits.</param>
    [TestCase(1, 0)]
    [TestCase(5, 0)]
    [TestCase(6, 25)]
    [TestCase(10, 25)]
    [TestCase(11, 50)]
    [TestCase(20, 50)]
    [TestCase(21, 75)]
    [TestCase(30, 75)]
    [TestCase(31, 100)]
    [TestCase(40, 100)]
    [TestCase(41, 150)]
    [TestCase(50, 150)]
    public void FruitsPerReset(int resetNumber, int expectedFruits)
    {
        var changed = ResetFruitTiersUpdatePlugIn.ReplaceItemCostTiers(LiveConfiguration)!;
        var tiers = JsonNode.Parse(changed)!["ItemCostTiers"]!["$values"]!.AsArray();
        Assert.That(tiers, Has.Count.EqualTo(5));

        var configuration = new ResetConfiguration { PayItemCostsWithJewelBankFruits = true };
        foreach (var tier in tiers)
        {
            configuration.ItemCostTiers.Add(new ResetConfiguration.ResetItemCostTier
            {
                MinimumResetCount = tier!["MinimumResetCount"]!.GetValue<int>(),
                RequiredItemAmount = tier["RequiredItemAmount"]!.GetValue<int>(),
                AdditionalItemAmountPerReset = tier["AdditionalItemAmountPerReset"]!.GetValue<int>(),
            });
        }

        var progression = ResetProgressionCalculator.Calculate(resetNumber - 1, 0, configuration);

        Assert.That(progression.RequiredItemAmount, Is.EqualTo(expectedFruits));
    }

    /// <summary>
    /// Tests that an unexpected stored shape is left alone.
    /// </summary>
    [Test]
    public void UnexpectedShapeIsNotChanged()
    {
        Assert.That(ResetFruitTiersUpdatePlugIn.ReplaceItemCostTiers("""{"ResetLimit": 50}"""), Is.Null);
        Assert.That(ResetFruitTiersUpdatePlugIn.ReplaceItemCostTiers("""{"ItemCostTiers": []}"""), Is.Null);
    }

    /// <summary>
    /// Tests that the installer changes the stored configuration once and leaves its other values alone.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task InstallerChangesTheStoredConfigurationOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        var typeId = typeof(ResetFeaturePlugIn).GUID;

        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var plugInConfiguration = configuration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeId);
            if (plugInConfiguration is null)
            {
                plugInConfiguration = context.CreateNew<PlugInConfiguration>();
                plugInConfiguration.TypeId = typeId;
                configuration.PlugInConfigurations.Add(plugInConfiguration);
            }

            plugInConfiguration.CustomConfiguration = LiveConfiguration;

            // Puts the database back into the state before the update, like the live database is.
            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.ResetFruitTiers).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await ResetFruitTiersInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True, "first start");
        Assert.That(await ResetFruitTiersInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "second start");

        using var reloaded = provider.CreateNewContext();
        var result = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var json = JsonNode.Parse(result.PlugInConfigurations.Single(c => c.TypeId == typeId).CustomConfiguration!)!.AsObject();
        Assert.That(json["ItemCostTiers"]!["$values"]!.AsArray(), Has.Count.EqualTo(5));
        Assert.That(json["ResetLimit"]!.GetValue<int>(), Is.EqualTo(50));
        Assert.That(json["PayItemCostsWithJewelBankFruits"]!.GetValue<bool>(), Is.True);
        Assert.That(json["MoneyCostTiers"]!["$values"]!.AsArray(), Has.Count.EqualTo(2));
    }
}
