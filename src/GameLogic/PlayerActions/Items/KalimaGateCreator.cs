// <copyright file="KalimaGateCreator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Items;

using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Pathfinding;
using MonsterSpawnArea = MUnique.OpenMU.Persistence.BasicModel.MonsterSpawnArea;

/// <summary>
/// Creates the same Kalima portal for Lost Maps and reusable tickets.
/// </summary>
internal static class KalimaGateCreator
{
    private const byte GateNpcStartNumber = 152;
    private static readonly TimeSpan PortalLifetime = TimeSpan.FromMinutes(1);
    private static readonly int[] KalimaMapNumbers = [24, 25, 26, 27, 28, 29, 36];
    private static readonly int[] NormalMinimumLevels = [40, 131, 181, 231, 281, 331, 350];
    private static readonly int[] SpecialMinimumLevels = [20, 111, 161, 211, 261, 311, 350];

    internal static bool IsKalima(int mapNumber) => KalimaMapNumbers.Contains(mapNumber);

    internal static byte GetLevel(Player player)
    {
        var minimumLevels = player.SelectedCharacter!.IsSpecialCharacter() ? SpecialMinimumLevels : NormalMinimumLevels;
        for (var index = minimumLevels.Length - 1; index >= 0; index--)
        {
            if (player.Level >= minimumLevels[index])
            {
                return (byte)(index + 1);
            }
        }

        return 0;
    }

    internal static async ValueTask<bool> CreateAsync(Player player, byte level, Point target)
    {
        var currentMap = player.CurrentMap;
        if (currentMap is null || !await CanCreateAsync(player, level, target).ConfigureAwait(false))
        {
            return false;
        }

        var gateNpcNumber = GateNpcStartNumber + level - 1;
        var gateNpcDef = player.GameContext.Configuration.Monsters.FirstOrDefault(def => def.Number == gateNpcNumber);
        if (gateNpcDef is null)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.UndefinedGateNpc)).ConfigureAwait(false);
            return false;
        }

        var targetGate = player.GameContext.Configuration.Maps.FirstOrDefault(g => g.Number == KalimaMapNumbers[level - 1])?.ExitGates.FirstOrDefault();
        if (targetGate is null)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.KalimaEntranceNotFound)).ConfigureAwait(false);
            return false;
        }

        var gate = new GateNpc(CreateSpawnArea(gateNpcDef, target), gateNpcDef, currentMap, player, targetGate, PortalLifetime);
        gate.Initialize();
        await currentMap.AddAsync(gate).ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> CanCreateAsync(Player player, byte level, Point target)
    {
        if (level is < 1 or > 7)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.InvalidLostMap)).ConfigureAwait(false);
            return false;
        }

        if (player.CurrentMiniGame is not null)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.NoKalimaGateOnEventMap)).ConfigureAwait(false);
            return false;
        }

        var gatePosition = target;
        if (player.IsAtSafezone() || player.CurrentMap?.Terrain.SafezoneMap[gatePosition.X, gatePosition.Y] is true)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.NoKalimaGateInSafezone)).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private static MonsterSpawnArea CreateSpawnArea(MonsterDefinition gateNpcDef, Point target)
    {
        return new MonsterSpawnArea
        {
            Direction = Direction.West,
            Quantity = 1,
            MonsterDefinition = gateNpcDef,
            SpawnTrigger = SpawnTrigger.ManuallyForEvent,
            X1 = target.X,
            X2 = target.X,
            Y1 = target.Y,
            Y2 = target.Y,
        };
    }
}
