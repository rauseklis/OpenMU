// <copyright file="DarkSkeletonMonsters.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Skills;

/// <summary>
/// Defines the dark skeletons (monsters 1111-1112), custom monsters of this server: 20% bigger, dark skeletons.
/// Every kill drops one box: a Box of Heaven (90%) or a chocolate box (10%; Pink for Dark Skeleton, Blue for Dark Reaper).
/// Dark Skeleton carries a Gorgon Staff and its attacks are shown as Evil Spirit; Dark Reaper carries a Rune Blade and its
/// attacks are shown as Hellfire. The client draws them (size, darkness, sparks, weapon); the server only knows their
/// values and where they spawn. For now one of each stands in Devias for testing.
/// </summary>
internal static class DarkSkeletonMonsters
{
    /// <summary>The number of the Dark Skeleton monster (Gorgon Staff, Evil Spirit).</summary>
    internal const short DarkSkeletonNumber = 1111;

    /// <summary>The number of the Dark Reaper monster (Rune Blade, Hellfire).</summary>
    internal const short DarkReaperNumber = 1112;

    // The owner's values for both: 10 000 health, everything else 100.
    private const int Level = 100;
    private const int MaximumHealth = 10000;
    private const int Damage = 100;
    private const int Defense = 100;
    private const int AttackRate = 100;
    private const int DefenseRate = 100;

    private const byte MoveRange = 3;
    private const byte AttackRange = 1;
    private const short ViewRange = 5;
    private const int MoveDelayMilliseconds = 400;
    private const int AttackDelayMilliseconds = 1600;
    private const byte FireAttribute = 2;

    // Devias spawn areas 1-9 are NPCs and 100-108 the original monsters; ours start well above them.
    private const short FirstDeviasSpawnNumber = 200;
    private const short DeviasQuantity = 1;

    // Drops (the owner's choice): every kill drops exactly one box, a Box of Heaven (Box of Luck +7) in 90% of the
    // kills, a chocolate box in the other 10%; nothing else (no zen, no map drops, no bonuses).
    private const byte BoxGroup = 14;
    private const short BoxOfLuckNumber = 11;
    private const byte BoxOfHeavenLevel = 7;
    private const double BoxOfHeavenChance = 0.9;
    private const double ChocolateBoxChance = 0.1;
    private const short PinkChocolateBoxNumber = 32;
    private const short BlueChocolateBoxNumber = 34;

    /// <summary>
    /// Gets the dark skeletons with the values and the Devias test spot the owner chose.
    /// </summary>
    internal static IReadOnlyList<DarkSkeleton> All { get; } =
    [
        new(DarkSkeletonNumber, "Dark Skeleton", SkillNumber.EvilSpirit, 10, 221, 81, PinkChocolateBoxNumber, "Pink Chocolate Box"),
        new(DarkReaperNumber, "Dark Reaper", SkillNumber.Hellfire, 20, 222, 85, BlueChocolateBoxNumber, "Blue Chocolate Box"),
    ];

    /// <summary>
    /// Adds the dark skeleton monster definitions which don't exist yet.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The number of added monsters.</returns>
    internal static int AddMissing(IContext context, GameConfiguration gameConfiguration)
    {
        var added = 0;
        foreach (var skeleton in All)
        {
            if (gameConfiguration.Monsters.Any(m => m.Number == skeleton.Number))
            {
                continue;
            }

            Add(context, gameConfiguration, skeleton);
            added++;
        }

        return added;
    }

    /// <summary>
    /// Adds the box drops (Box of Heaven 90%, chocolate box 10%) to the dark skeletons which don't have drops of their own yet.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The number of monsters which got their drops.</returns>
    internal static int AddMissingDrops(IContext context, GameConfiguration gameConfiguration)
    {
        var added = 0;
        foreach (var skeleton in All)
        {
            if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == skeleton.Number) is not { } monster
                || monster.DropItemGroups.Count > 0)
            {
                continue;
            }

            AddDrops(context, gameConfiguration, monster, skeleton);
            added++;
        }

        return added;
    }

    /// <summary>
    /// Adds the Devias test spawns (one of each monster) which don't exist yet. A monster which isn't defined is skipped.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="devias">The Devias map definition.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The number of added spawn areas.</returns>
    internal static int AddDeviasSpawns(IContext context, GameMapDefinition devias, GameConfiguration gameConfiguration)
    {
        var added = 0;
        var spawnNumber = FirstDeviasSpawnNumber;
        foreach (var skeleton in All)
        {
            var currentSpawnNumber = spawnNumber++;
            if (devias.MonsterSpawns.Any(a => a.MonsterDefinition?.Number == skeleton.Number))
            {
                continue;
            }

            if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == skeleton.Number) is not { } monster)
            {
                continue;
            }

            AddSpawn(context, devias, monster, skeleton, currentSpawnNumber);
            added++;
        }

        return added;
    }

    private static void AddSpawn(IContext context, GameMapDefinition devias, MonsterDefinition monster, DarkSkeleton skeleton, short spawnNumber)
    {
        var area = context.CreateNew<MonsterSpawnArea>();
        area.SetGuid(devias.Number, spawnNumber);
        area.GameMap = devias;
        area.MonsterDefinition = monster;
        area.Quantity = DeviasQuantity;
        area.Direction = Direction.Undefined;
        area.SpawnTrigger = SpawnTrigger.Automatic;
        area.X1 = skeleton.DeviasX;
        area.X2 = skeleton.DeviasX;
        area.Y1 = skeleton.DeviasY;
        area.Y2 = skeleton.DeviasY;
        devias.MonsterSpawns.Add(area);
    }

    private static void Add(IContext context, GameConfiguration gameConfiguration, DarkSkeleton skeleton)
    {
        var monster = context.CreateNew<MonsterDefinition>();
        gameConfiguration.Monsters.Add(monster);
        monster.Number = skeleton.Number;
        monster.Designation = skeleton.Name;
        monster.MoveRange = MoveRange;
        monster.AttackRange = AttackRange;
        monster.ViewRange = ViewRange;
        monster.MoveDelay = TimeSpan.FromMilliseconds(MoveDelayMilliseconds);
        monster.AttackDelay = TimeSpan.FromMilliseconds(AttackDelayMilliseconds);
        monster.RespawnDelay = TimeSpan.FromSeconds(skeleton.RespawnSeconds);
        monster.Attribute = FireAttribute;

        // The hit itself is the normal monster attack on its target; the skill only decides what is shown.
        monster.AttackSkill = gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)skeleton.Skill);

        var attributes = new Dictionary<AttributeDefinition, float>
        {
            { Stats.Level, Level },
            { Stats.MaximumHealth, MaximumHealth },
            { Stats.MinimumPhysBaseDmg, Damage },
            { Stats.MaximumPhysBaseDmg, Damage },
            { Stats.DefenseBase, Defense },
            { Stats.AttackRatePvm, AttackRate },
            { Stats.DefenseRatePvm, DefenseRate },
        };
        monster.AddAttributes(attributes, context, gameConfiguration);
    }

    private static void AddDrops(IContext context, GameConfiguration gameConfiguration, MonsterDefinition monster, DarkSkeleton skeleton)
    {
        monster.NumberOfMaximumItemDrops = 1;
        AddDropGroup(context, gameConfiguration, monster, "Box of Heaven", BoxOfHeavenChance, BoxOfHeavenLevel, BoxOfLuckNumber);
        AddDropGroup(context, gameConfiguration, monster, skeleton.ChocolateBoxName, ChocolateBoxChance, 0, skeleton.ChocolateBoxNumber);
    }

    private static void AddDropGroup(IContext context, GameConfiguration gameConfiguration, MonsterDefinition monster, string name, double chance, byte itemLevel, short itemNumber)
    {
        var group = context.CreateNew<DropItemGroup>();
        group.Description = $"{monster.Designation}: {name}";
        group.Chance = chance;
        group.ItemLevel = itemLevel;
        group.ItemType = SpecialItemType.None;
        group.Monster = monster;
        group.PossibleItems.Add(gameConfiguration.Items.First(item => item.Group == BoxGroup && item.Number == itemNumber));
        monster.DropItemGroups.Add(group);
        gameConfiguration.DropItemGroups.Add(group);
    }

    /// <summary>
    /// One dark skeleton: its number, name, the skill its attacks are shown as, its respawn time and its Devias test spot.
    /// </summary>
    /// <param name="Number">The monster number.</param>
    /// <param name="Name">The monster name.</param>
    /// <param name="Skill">The skill which is shown when it attacks.</param>
    /// <param name="RespawnSeconds">The respawn time in seconds.</param>
    /// <param name="DeviasX">The x coordinate of the Devias test spawn.</param>
    /// <param name="DeviasY">The y coordinate of the Devias test spawn.</param>
    /// <param name="ChocolateBoxNumber">The item number (group 14) of the chocolate box it drops in 10% of the kills.</param>
    /// <param name="ChocolateBoxName">The name of that chocolate box.</param>
    internal readonly record struct DarkSkeleton(short Number, string Name, SkillNumber Skill, int RespawnSeconds, byte DeviasX, byte DeviasY, short ChocolateBoxNumber, string ChocolateBoxName);
}
