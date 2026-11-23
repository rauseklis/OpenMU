// <copyright file="BonusLevelSources.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Decides which monsters drop the small bonuses by monster level instead of by hand-picked monster lists.
/// Every monster that spawns on a map (including Kalima, Blood Castle and Devil Square) and lies inside the level
/// range of a bonus gets that bonus with the bonus' absolute chance per kill.
/// </summary>
public sealed class BonusLevelSources(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    private const byte WrapperGroup = 14;
    private const short WrapperNumber = 11;

    private static readonly SpawnTrigger[] EligibleTriggers =
    [
        SpawnTrigger.Automatic,
        SpawnTrigger.AutomaticDuringEvent,
        SpawnTrigger.OnceAtEventStart,
        SpawnTrigger.AutomaticDuringWave,
    ];

    private static readonly Band[] Bands =
    [
        new("Star", 1, 72, 99, .0005),
        new("Firecracker", 2, 60, 74, .0015),
        new("Heart of Love", 3, 50, 60, .003),
        new("Silver Medal", 5, 1, 44, .01),
        new("Gold Medal", 6, 45, 59, .0035),
        new("Box of Heaven", 7, 20, 74, .0005),
    ];

    /// <inheritdoc />
    public override void Initialize()
    {
        var wrapper = this.GameConfiguration.Items.Single(item => item.Group == WrapperGroup && item.Number == WrapperNumber);
        var eventRewards = this.GameConfiguration.MiniGameDefinitions.SelectMany(game => game.Rewards)
            .Where(reward => reward.ItemReward is not null).Select(reward => reward.ItemReward!).ToHashSet();
        this.RemoveOldSources(eventRewards);

        var mapsByMonster = new SortedDictionary<short, (MonsterDefinition Monster, List<GameMapDefinition> Maps)>();
        foreach (var map in this.GameConfiguration.Maps.Where(map => map.BattleZone is null).OrderBy(map => map.Number))
        {
            foreach (var monster in map.MonsterSpawns.Where(IsEligibleSpawn).Select(spawn => spawn.MonsterDefinition!).Distinct())
            {
                if (!mapsByMonster.TryGetValue(monster.Number, out var entry))
                {
                    entry = (monster, []);
                    mapsByMonster.Add(monster.Number, entry);
                }

                entry.Maps.Add(map);
            }
        }

        foreach (var (monster, maps) in mapsByMonster.Values)
        {
            var level = GetLevel(monster);
            foreach (var band in Bands.Where(band => level >= band.Minimum && level <= band.Maximum))
            {
                var group = this.Context.CreateNew<DropItemGroup>();
                group.Description = $"{band.Name} / level {band.Minimum}–{band.Maximum} / {monster.Designation}";
                group.ItemType = SpecialItemType.SmallBonus;
                group.ItemLevel = band.Level;
                group.Chance = band.Chance;
                group.Monster = monster;
                group.MinimumMonsterLevel = band.Minimum;
                group.MaximumMonsterLevel = band.Maximum;
                group.PossibleItems.Add(wrapper);
                this.GameConfiguration.DropItemGroups.Add(group);
                foreach (var map in maps)
                {
                    map.DropItemGroups.Add(group);
                }
            }
        }
    }

    private static bool IsEligibleSpawn(MonsterSpawnArea spawn) =>
        spawn.Quantity > 0
        && EligibleTriggers.Contains(spawn.SpawnTrigger)
        && spawn.MonsterDefinition is { ObjectKind: NpcObjectKind.Monster, NumberOfMaximumItemDrops: > 0 } monster
        && monster.Attributes.Any(attribute => attribute.AttributeDefinition == Stats.Level)
        && !monster.DropItemGroups.Any(group => group.ItemType != SpecialItemType.SmallBonus && group.Chance >= 1);

    private static float GetLevel(MonsterDefinition monster) =>
        monster.Attributes.First(attribute => attribute.AttributeDefinition == Stats.Level).Value;

    private void RemoveOldSources(HashSet<DropItemGroup> eventRewards)
    {
        var old = this.GameConfiguration.DropItemGroups.Where(group => group.ItemType == SpecialItemType.SmallBonus
            && !eventRewards.Contains(group)
            && group.PossibleItems.Count == 1
            && group.PossibleItems.Single() is { Group: WrapperGroup, Number: WrapperNumber }).ToArray();
        foreach (var group in old)
        {
            foreach (var map in this.GameConfiguration.Maps)
            {
                map.DropItemGroups.Remove(group);
            }

            foreach (var monster in this.GameConfiguration.Monsters)
            {
                monster.DropItemGroups.Remove(group);
            }

            this.GameConfiguration.DropItemGroups.Remove(group);
        }
    }

    private sealed record Band(string Name, byte Level, byte Minimum, byte Maximum, double Chance);
}
