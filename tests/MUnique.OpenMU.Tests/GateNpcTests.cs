// <copyright file="GateNpcTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// Tests for the lifecycle of <see cref="GateNpc"/>.
/// </summary>
[TestFixture]
public class GateNpcTests
{
    /// <summary>
    /// Verifies that the lifespan removes the gate from the map.
    /// </summary>
    [Test]
    public async ValueTask ExpiredGateIsRemovedFromMapAsync()
    {
        var (gate, map) = await CreateGateAsync(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

        await WaitUntilRemovedAsync(gate, map).ConfigureAwait(false);

        Assert.That(map.GetObject(gate.Id), Is.Null);
    }

    /// <summary>
    /// Verifies that an external asynchronous disposal can stop the worker without deadlocking
    /// or disposing its cancellation source twice.
    /// </summary>
    [Test]
    public async ValueTask ExternalDisposalCompletesAndRemovesGateAsync()
    {
        var (gate, map) = await CreateGateAsync(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

        await gate.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

        Assert.That(map.GetObject(gate.Id), Is.Null);
    }

    private static async ValueTask<(GateNpc Gate, GameMap Map)> CreateGateAsync(TimeSpan lifespan)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var map = await player.GameContext.GetMapAsync(0).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Test map could not be created.");
        var gate = new GateNpc(
            new MonsterSpawnArea(),
            new MonsterDefinition(),
            map,
            player,
            new ExitGate(),
            lifespan);

        await map.AddAsync(gate).ConfigureAwait(false);
        return (gate, map);
    }

    private static async ValueTask WaitUntilRemovedAsync(GateNpc gate, GameMap map)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (map.GetObject(gate.Id) is not null)
        {
            await Task.Delay(10, timeout.Token).ConfigureAwait(false);
        }
    }
}
