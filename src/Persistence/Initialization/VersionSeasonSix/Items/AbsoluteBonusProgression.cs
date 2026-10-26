// <copyright file="AbsoluteBonusProgression.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Configures absolute bonus chances, level-derived ordinary Silver sources and fruit-free Heaven rewards.
/// </summary>
public sealed class AbsoluteBonusProgression(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    private const byte SilverLevel = 5;
    private const byte HeavenLevel = 7;
    private const byte MinimumSilverMonsterLevel = 1;
    private const byte MaximumSilverMonsterLevel = 44;
    private const double SilverChance = .01;

    /// <inheritdoc />
    public override void Initialize()
    {
        foreach (var group in this.GameConfiguration.DropItemGroups.Where(IsReviewedBonus))
        {
            group.Chance = group.ItemLevel switch
            {
                1 => .0005,
                2 => .0015,
                3 => .0035,
                5 => SilverChance,
                6 => .005,
                7 => .0025,
                _ => throw new InvalidOperationException("Unexpected reviewed bonus level."),
            };
        }

        this.ConfigureSilverSources();
        this.ConfigureHeaven();
    }

    private static bool IsReviewedBonus(DropItemGroup group) => group.ItemType == SpecialItemType.SmallBonus
        && group.ItemLevel is 1 or 2 or 3 or 5 or 6 or 7
        && group.PossibleItems.Count == 1 && group.PossibleItems.Single() is { Group: 14, Number: 11 };

    private static bool IsOrdinarySilverSpawn(MonsterSpawnArea spawn)
    {
        if (spawn.SpawnTrigger != SpawnTrigger.Automatic || spawn.Quantity <= 0
            || spawn.MonsterDefinition is not { ObjectKind: NpcObjectKind.Monster, NumberOfMaximumItemDrops: > 0 } monster)
        {
            return false;
        }

        return monster.Attributes.Any(attribute => attribute.AttributeDefinition == Stats.Level
                && attribute.Value is >= MinimumSilverMonsterLevel and <= MaximumSilverMonsterLevel)
            && !monster.DropItemGroups.Any(group => group.ItemType != SpecialItemType.SmallBonus && group.Chance >= 1);
    }

    private void ConfigureSilverSources()
    {
        foreach (var old in this.GameConfiguration.DropItemGroups.Where(group => IsReviewedBonus(group) && group.ItemLevel == SilverLevel).ToArray())
        {
            foreach (var map in this.GameConfiguration.Maps)
            {
                map.DropItemGroups.Remove(old);
            }

            foreach (var monster in this.GameConfiguration.Monsters)
            {
                monster.DropItemGroups.Remove(old);
            }

            this.GameConfiguration.DropItemGroups.Remove(old);
        }

        var eventMaps = this.GameConfiguration.MiniGameDefinitions.Select(game => game.Entrance?.Map).OfType<GameMapDefinition>().ToHashSet();
        foreach (var map in this.GameConfiguration.Maps.Where(map => !eventMaps.Contains(map) && map.BattleZone is null))
        {
            foreach (var monster in map.MonsterSpawns.Where(IsOrdinarySilverSpawn).Select(spawn => spawn.MonsterDefinition!).Distinct())
            {
                var group = this.Context.CreateNew<DropItemGroup>();
                group.Description = $"Silver Medal / ordinary level 1–44 / {monster.Designation}";
                group.ItemType = SpecialItemType.SmallBonus;
                group.ItemLevel = SilverLevel;
                group.Chance = SilverChance;
                group.Monster = monster;
                group.MinimumMonsterLevel = MinimumSilverMonsterLevel;
                group.MaximumMonsterLevel = MaximumSilverMonsterLevel;
                group.PossibleItems.Add(this.Item(14, 11));
                this.GameConfiguration.DropItemGroups.Add(group);
                map.DropItemGroups.Add(group);
            }
        }
    }

    private void ConfigureHeaven()
    {
        var box = this.Item(14, 11);
        foreach (var old in box.DropItems.Where(group => group.SourceItemLevel == HeavenLevel).ToArray())
        {
            box.DropItems.Remove(old);
        }

        this.AddHeavenGroup(box, "jewels", .45, [(14, 13), (14, 14)]);
        this.AddHeavenGroup(box, "pets", .30, [(13, 1), (13, 0), (13, 2)]);
        this.AddHeavenGroup(box, "higher pets", .05, [(13, 64), (13, 65), (13, 3)]);
        var zen = this.AddHeavenGroup(box, "zen", .20, []);
        zen.ItemType = SpecialItemType.Money;
        zen.MoneyAmount = 100_000;
    }

    private ItemDropItemGroup AddHeavenGroup(ItemDefinition box, string category, double chance, (byte Group, short Number)[] items)
    {
        var group = this.Context.CreateNew<ItemDropItemGroup>();
        group.Description = $"Box of Heaven / {category}";
        group.SourceItemLevel = HeavenLevel;
        group.Chance = chance;
        group.ItemType = SpecialItemType.RandomItem;
        group.DropEffect = ItemDropEffect.ChristmasFireworks;
        foreach (var key in items)
        {
            group.PossibleItems.Add(this.Item(key.Group, key.Number));
        }

        box.DropItems.Add(group);
        return group;
    }

    private ItemDefinition Item(byte group, short number) => this.GameConfiguration.Items.Single(item => item.Group == group && item.Number == number);
}
