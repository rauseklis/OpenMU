// <copyright file="CrywolfPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.ComponentModel;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Crywolf;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using ItemDefinition = MUnique.OpenMU.Persistence.BasicModel.ItemDefinition;

/// <summary>
/// Tests the benefits and penalties of the <see cref="CrywolfPlugIn"/>, which apply on all game servers.
/// </summary>
[TestFixture]
public class CrywolfPlugInTest
{
    /// <summary>
    /// Tests that deactivating the plugin during the war ends the war, so that the other game servers don't stay in it.
    /// </summary>
    [Test]
    public async Task DeactivationDuringTheWarEndsTheWarAsync()
    {
        var gameContext = (GameContext)GameContextTestHelper.CreateGameContext();
        var map = (await gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        var definition = new CrywolfEventDefinition { MapNumber = 0, MonsterGroups = new List<CrywolfMonsterGroup>() };
        await AddNpcAsync(map, definition.StatueNumber).ConfigureAwait(false);
        foreach (var altarNumber in definition.AltarNumbers)
        {
            await AddNpcAsync(map, altarNumber).ConfigureAwait(false);
        }

        var plugIn = new CrywolfPlugIn { Configuration = definition };
        await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        var context = plugIn.GetOwnContext(gameContext)!;
        for (var i = 0; i < 10 && context.Occupation != CrywolfOccupationState.War; i++)
        {
            plugIn.ForceStart();
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        }

        Assert.That(context.Occupation, Is.EqualTo(CrywolfOccupationState.War), "precondition");

        gameContext.PlugInManager.RegisterPlugIn<IPeriodicTaskPlugIn, CrywolfPlugIn>();
        gameContext.PlugInManager.DeactivatePlugIn<CrywolfPlugIn>();

        for (var i = 0; i < 100 && context.State != CrywolfState.None; i++)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        Assert.That(context.State, Is.EqualTo(CrywolfState.None));
        Assert.That(context.Occupation, Is.Not.EqualTo(CrywolfOccupationState.War));
    }

    private static async ValueTask AddNpcAsync(GameMap map, short number)
    {
        var npcDefinition = new MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition { Id = Guid.NewGuid(), Number = number, ObjectKind = NpcObjectKind.PassiveNpc };
        var spawnArea = new MUnique.OpenMU.Persistence.BasicModel.MonsterSpawnArea
        {
            MonsterDefinition = npcDefinition,
            GameMap = map.Definition,
            X1 = (byte)(120 + (number % 10)),
            X2 = (byte)(120 + (number % 10)),
            Y1 = 30,
            Y2 = 30,
            Quantity = 1,
        };
        var npc = new NonPlayerCharacter(spawnArea, npcDefinition, map);
        npc.Initialize();
        await map.AddAsync(npc).ConfigureAwait(false);
    }

    private static async ValueTask<(CrywolfPlugIn PlugIn, GameContext GameContext)> CreatePlugInAsync(bool isOccupied, Action<CrywolfEventDefinition> configure, bool execute = true)
    {
        var gameContext = (GameContext)GameContextTestHelper.CreateGameContext();
        using (var persistenceContext = gameContext.PersistenceContextProvider.CreateNewTypedContext(typeof(CrywolfData), false, gameContext.Configuration))
        {
            var data = persistenceContext.CreateNew<CrywolfData>();
            data.IsOccupied = isOccupied;
            data.LastBattleEnd = DateTime.UtcNow;
            await persistenceContext.SaveChangesAsync().ConfigureAwait(false);
        }

        // The map of the event doesn't exist in the test configuration. The occupation state applies anyway.
        var definition = new CrywolfEventDefinition { MapNumber = 999, IsPenaltyActive = true };
        configure(definition);
        var plugIn = new CrywolfPlugIn { Configuration = definition };
        if (execute)
        {
            await plugIn.ExecuteTaskAsync(gameContext).ConfigureAwait(false);
        }

        return (plugIn, gameContext);
    }
}
