// <copyright file="CrustRespawnDelayTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests that the Mega Crust and the Alpha Crust respawn after 10 seconds, like the Alquamos.
/// </summary>
[TestFixture]
public class CrustRespawnDelayTests
{
    private static readonly TimeSpan Expected = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Tests that the update changes both Crusts and leaves other monsters alone.
    /// </summary>
    [Test]
    public async Task UpdateSetsBothCrustsToTenSeconds()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>();
        foreach (var number in new short[] { 71, 74, 72 })
        {
            var monster = context.CreateNew<MonsterDefinition>();
            monster.Number = number;
            monster.RespawnDelay = TimeSpan.FromSeconds(3);
            configuration.Monsters.Add(monster);
        }

        await new CrustRespawnDelayUpdatePlugIn().ApplyUpdateAsync(context, configuration);

        Assert.That(configuration.Monsters.Single(m => m.Number == 71).RespawnDelay, Is.EqualTo(Expected));
        Assert.That(configuration.Monsters.Single(m => m.Number == 74).RespawnDelay, Is.EqualTo(Expected));
        Assert.That(configuration.Monsters.Single(m => m.Number == 72).RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(3)));
    }

    /// <summary>
    /// Tests that a brand-new database already has the Crusts at the Alquamos' delay.
    /// </summary>
    [Test]
    public async Task NewDatabaseGivesTheCrustsTheAlquamosDelay()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(contextProvider, new NullLoggerFactory()).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        var configuration = (await contextProvider.CreateNewConfigurationContext().GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        var alquamos = configuration.Monsters.Single(m => m.Number == 69).RespawnDelay;
        Assert.That(configuration.Monsters.Single(m => m.Number == 71).RespawnDelay, Is.EqualTo(alquamos));
        Assert.That(configuration.Monsters.Single(m => m.Number == 74).RespawnDelay, Is.EqualTo(alquamos));
    }
}
