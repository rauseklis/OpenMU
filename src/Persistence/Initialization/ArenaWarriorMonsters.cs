// <copyright file="ArenaWarriorMonsters.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Skills;

/// <summary>
/// Defines the eleven Arena Warrior monsters (1100-1110), ported with the exact values of the
/// "Aetherion S6E19" Monster.txt. They spawn in the cages of the Arena map.
/// </summary>
internal static class ArenaWarriorMonsters
{
    /// <summary>The number of the first Arena Warrior monster.</summary>
    internal const short FirstNumber = 1100;

    /// <summary>The number of the last Arena Warrior monster.</summary>
    internal const short LastNumber = 1110;

    // Spawn areas of the Arena map 1-3 are the soccer NPCs; ours start well above them.
    private const short FirstSpawnNumber = 100;

    // Columns of the source file: Level, MaxLife, DamageMin, DamageMax, Defense, AttackRate, DefenseRate,
    // MoveRange, AttackType, AttackRange, ViewRange, MoveSpeed, AttackSpeed, RegenTime, Attribute,
    // Resi(Poison), Resi(Ice), Resi(Light), Resis(Fire), then the attack skill the owner chose: Guardian Elf Triple Shot (the
    // crossbow skill), Sphinx Wizzard Meteorite, Soul Wizzard Lightning; the knights have none (plain melee).
    // MaxMana and MagicDefense are 0 for all of them.
    private static readonly ArenaWarrior[] Warriors =
    [
        new(1100, "Scale Warrior", 82, 19000, 305, 350, 230, 1095, 187, 3, 0, 2, 5, 400, 1400, 10, 2, 9, 9, 11, 9, null),
        new(1101, "Brass Warrior", 96, 41000, 560, 610, 425, 1473, 326, 3, 0, 2, 5, 400, 1400, 10, 2, 9, 9, 12, 9, null),
        new(1102, "Plate Warrior", 92, 34500, 489, 540, 360, 1182, 287, 3, 0, 2, 5, 400, 1400, 10, 2, 12, 12, 13, 1, null),
        new(1103, "Red Dragon Warrior", 105, 48500, 640, 675, 515, 1191, 358, 4, 0, 2, 7, 450, 1500, 10, 2, 21, 21, 21, 2, null),
        new(1104, "Black Dragon Knight", 117, 87500, 830, 1085, 865, 1642, 555, 4, 0, 2, 7, 450, 1700, 10, 2, 22, 22, 22, 2, null),
        new(1105, "Phoenix Knight", 123, 184370, 915, 990, 543, 1557, 755, 6, 0, 2, 10, 600, 2000, 10, 2, 50, 50, 50, 5, null),
        new(1106, "Guardian Elf", 108, 72000, 685, 735, 545, 1120, 315, 3, 0, 5, 5, 400, 1400, 10, 2, 14, 14, 16, 1, SkillNumber.TripleShot),
        new(1107, "Sphinx Wizzard", 86, 29000, 425, 480, 305, 1134, 248, 3, 11, 5, 5, 400, 1400, 10, 2, 9, 11, 12, 9, SkillNumber.Meteorite),
        new(1108, "Soul Wizzard", 100, 50000, 650, 700, 495, 1966, 372, 4, 3, 5, 7, 450, 1700, 10, 2, 22, 22, 22, 2, SkillNumber.Lightning),
        new(1109, "Great Dragon Warrior", 128, 203000, 1046, 1181, 630, 1575, 750, 3, 0, 2, 7, 500, 1500, 10, 2, 25, 25, 25, 2, null),
        new(1110, "Dragon Knight Warrior", 140, 237000, 1741, 2317, 785, 2240, 1440, 3, 0, 2, 9, 700, 2500, 10, 2, 20, 20, 150, 2, null),
    ];

    // Eight monsters in each cage of the Arena (the walled cells of the map), spawning in a small cluster: the area is a
    // 4x4 patch placed at a different spot of its cage, and every monster picks a random tile of it. The cages nearest the
    // entrance in the south hold the weaker monsters, the north ones the stronger: 6 weak, 5 medium and 4 hard cages.
    // Monsters repeat. Monster, X1, X2, Y1, Y2, quantity, direction (0 = random).
    private static readonly ArenaSpawn[] Spawns =
    [
        new(1100, 9, 12, 88, 91, 8, 0),
        new(1107, 27, 30, 90, 93, 8, 0),
        new(1101, 48, 51, 93, 96, 8, 0),
        new(1102, 10, 13, 75, 78, 8, 0),
        new(1108, 29, 32, 73, 76, 8, 0),
        new(1103, 46, 49, 71, 74, 8, 0),
        new(1106, 10, 13, 55, 58, 8, 0),
        new(1104, 26, 29, 53, 56, 8, 0),
        new(1105, 48, 51, 54, 57, 8, 0),
        new(1109, 9, 12, 37, 40, 8, 0),
        new(1110, 26, 29, 39, 42, 8, 0),
        new(1105, 45, 48, 37, 40, 8, 0),
        new(1100, 66, 69, 83, 86, 8, 0),
        new(1107, 65, 68, 73, 76, 8, 0),
        new(1103, 67, 70, 65, 67, 8, 0),
    ];

    /// <summary>
    /// Adds the Arena Warrior spawn areas to the Arena map, unless the map already has spawns of these monsters.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="arena">The Arena map definition.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void AddSpawns(IContext context, GameMapDefinition arena, GameConfiguration gameConfiguration)
    {
        if (arena.MonsterSpawns.Any(a => a.MonsterDefinition?.Number is >= FirstNumber and <= LastNumber))
        {
            return;
        }

        short number = FirstSpawnNumber;
        foreach (var spawn in Spawns)
        {
            var monster = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == spawn.Monster);
            if (monster is null)
            {
                continue;
            }

            var area = context.CreateNew<MonsterSpawnArea>();
            area.SetGuid(arena.Number, number++);
            area.GameMap = arena;
            area.MonsterDefinition = monster;
            area.Quantity = spawn.Quantity;
            area.Direction = (Direction)spawn.Direction;
            area.SpawnTrigger = SpawnTrigger.Automatic;
            area.X1 = spawn.X1;
            area.X2 = spawn.X2;
            area.Y1 = spawn.Y1;
            area.Y2 = spawn.Y2;
            arena.MonsterSpawns.Add(area);
        }
    }

    /// <summary>
    /// Adds the Arena Warrior monsters which are not yet part of the game configuration.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The number of added monsters.</returns>
    internal static int AddMissing(IContext context, GameConfiguration gameConfiguration)
    {
        var added = 0;
        foreach (var warrior in Warriors)
        {
            if (gameConfiguration.Monsters.Any(m => m.Number == warrior.Number))
            {
                continue;
            }

            Add(context, gameConfiguration, warrior);
            added++;
        }

        return added;
    }

    private static void Add(IContext context, GameConfiguration gameConfiguration, ArenaWarrior warrior)
    {
        var monster = context.CreateNew<MonsterDefinition>();
        gameConfiguration.Monsters.Add(monster);
        monster.Number = warrior.Number;
        monster.Designation = warrior.Name;
        monster.MoveRange = warrior.MoveRange;
        monster.AttackRange = warrior.AttackRange;
        monster.ViewRange = warrior.ViewRange;
        monster.MoveDelay = TimeSpan.FromMilliseconds(warrior.MoveSpeed);
        monster.AttackDelay = TimeSpan.FromMilliseconds(warrior.AttackSpeed);
        monster.RespawnDelay = TimeSpan.FromSeconds(warrior.RegenTime);
        monster.Attribute = warrior.Attribute;
        monster.NumberOfMaximumItemDrops = 1;
        if (warrior.Skill is { } skillNumber)
        {
            // The hit itself is the normal monster attack; the skill only decides what is shown (magic, meteor, shot).
            monster.AttackSkill = gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)skillNumber);
        }

        var attributes = new Dictionary<AttributeDefinition, float>
        {
            { Stats.Level, warrior.Level },
            { Stats.MaximumHealth, warrior.MaxLife },
            { Stats.MinimumPhysBaseDmg, warrior.DamageMin },
            { Stats.MaximumPhysBaseDmg, warrior.DamageMax },
            { Stats.DefenseBase, warrior.Defense },
            { Stats.AttackRatePvm, warrior.AttackRate },
            { Stats.DefenseRatePvm, warrior.DefenseRate },
            { Stats.PoisonResistance, warrior.PoisonResistance / 255f },
            { Stats.IceResistance, warrior.IceResistance / 255f },
            { Stats.LightningResistance, warrior.LightningResistance / 255f },
            { Stats.FireResistance, warrior.FireResistance / 255f },
        };
        monster.AddAttributes(attributes, context, gameConfiguration);
    }

    private readonly record struct ArenaSpawn(short Monster, byte X1, byte X2, byte Y1, byte Y2, short Quantity, byte Direction);

    private readonly record struct ArenaWarrior(
        short Number,
        string Name,
        int Level,
        int MaxLife,
        int DamageMin,
        int DamageMax,
        int Defense,
        int AttackRate,
        int DefenseRate,
        byte MoveRange,
        int AttackType,
        byte AttackRange,
        short ViewRange,
        int MoveSpeed,
        int AttackSpeed,
        int RegenTime,
        byte Attribute,
        int PoisonResistance,
        int IceResistance,
        int LightningResistance,
        int FireResistance,
        SkillNumber? Skill);
}
