// <copyright file="BoxRewardCatalogTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Validates the approved item pools against freshly initialized Season 6 configuration.
/// </summary>
[TestFixture]
public class BoxRewardCatalogTests
{
    /// <summary>
    /// Every listed item must exist; Kundun rewards must support Excellent and have no sockets.
    /// </summary>
    [Test]
    public async Task ApprovedCatalogContainsSupportedItemsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewConfigurationContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var items = config.Items.ToDictionary(item => (item.Group, item.Number));
        using var plan = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "box-reward-items.json")).ConfigureAwait(false));
        foreach (var box in plan.RootElement.EnumerateObject())
        {
            foreach (var category in box.Value.EnumerateObject())
            {
                foreach (var entry in category.Value.EnumerateArray())
                {
                    var key = (entry[0].GetByte(), entry[1].GetInt16());
                    Assert.That(items.ContainsKey(key), Is.True, $"{box.Name}/{category.Name}: missing {key}");
                    var item = items[key];
                    if (box.Name.StartsWith("BOX OF KUNDUN", StringComparison.Ordinal))
                    {
                        Assert.That(item.MaximumSockets, Is.Zero, $"Socket item in {box.Name}: {item.Name}");
                        Assert.That(item.PossibleItemOptions.SelectMany(option => option.PossibleOptions)
                            .Count(option => option.OptionType == ItemOptionTypes.Excellent), Is.GreaterThanOrEqualTo(2), item.Name.ToString());
                    }
                }
            }
        }
    }
}
