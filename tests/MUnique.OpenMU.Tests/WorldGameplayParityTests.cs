// <copyright file="WorldGameplayParityTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.Updates;

/// <summary>Protects shared gameplay settings and independent world experience rates.</summary>
[TestFixture]
public class WorldGameplayParityTests
{
    /// <summary>Every freshly initialized world receives the same bonus progression.</summary>
    [Test]
    public async Task BothFreshWorldsUseTheSameAbsoluteBonusDropsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(2, false);
        using var context = provider.CreateNewContext();
        var servers = (await context.GetAsync<GameServerDefinition>()).OrderBy(server => server.ServerID).ToArray();
        Assert.That(servers, Has.Length.EqualTo(2));
        Assert.That(servers.All(server => server.SmallBonusDropsEnabled), Is.True);
        Assert.That(servers.All(server => server.SmallBonusDropChance is null), Is.True);
    }
    /// <summary>The one-time upgrade preserves world EXP and later administrator edits.</summary>
    [Test]
    public async Task ExistingWorldsUpgradeOnceWithoutChangingRatesAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(2, false);
        using var context = provider.CreateNewContext();
        var servers = (await context.GetAsync<GameServerDefinition>()).OrderBy(server => server.ServerID).ToArray();
        servers[0].ExperienceRate = 50;
        servers[1].ExperienceRate = 5;
        servers[1].SmallBonusDropsEnabled = false;
        servers[1].SmallBonusDropChance = .05;
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>()).Where(update => update.Version == (int)UpdateVersion.WorldGameplayParity).ToArray())
        {
            await context.DeleteAsync(update);
        }
        await context.SaveChangesAsync();
        Assert.That(await WorldGameplayParityInstaller.ApplyIfNeededAsync(provider), Is.True);
        Assert.That(servers.All(server => server.SmallBonusDropsEnabled && server.SmallBonusDropChance is null), Is.True);
        Assert.That(servers.Select(server => server.ExperienceRate), Is.EqualTo(new[] { 50f, 5f }));
        servers[1].SmallBonusDropsEnabled = false;
        await context.SaveChangesAsync();
        Assert.That(await WorldGameplayParityInstaller.ApplyIfNeededAsync(provider), Is.False);
        Assert.That(servers[1].SmallBonusDropsEnabled, Is.False);
    }
}
