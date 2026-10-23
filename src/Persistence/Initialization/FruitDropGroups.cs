// <copyright file="FruitDropGroups.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Gives the monsters of Arena, Icarus, Aida and the three Kanturu maps a chance to drop each of the five fruits.
/// The groups are attached to the maps, so they apply to every monster there.
/// </summary>
public static class FruitDropGroups
{
    /// <summary>
    /// The chance of each single fruit type per item drop of a monster (3 %), so all five fruits together make 15 %.
    /// The default drop groups of a map add up to 80.11 %, so the total stays below 100 % and nothing else is diluted.
    /// </summary>
    public const double ChancePerFruit = 0.03;

    /// <summary>
    /// The item group of the fruits.
    /// </summary>
    public const byte FruitItemGroup = 13;

    /// <summary>
    /// The item number of the fruits.
    /// </summary>
    public const short FruitItemNumber = 15;

    /// <summary>
    /// The numbers of the maps whose monsters drop fruits: Arena, Icarus, Aida, Kanturu Ruins, Kanturu Relics and Kanturu Event.
    /// </summary>
    public static readonly IReadOnlyList<short> MapNumbers = [6, 10, 33, 37, 38, 39];

    /// <summary>
    /// The fruit types by their item level: Energy, Vitality, Agility, Strength, Command.
    /// </summary>
    public static readonly IReadOnlyList<string> FruitNames = ["Energy", "Vitality", "Agility", "Strength", "Command"];

    private const short GuidParentNumber = FruitItemGroup * 100 + FruitItemNumber;

    /// <summary>
    /// Makes sure the five fruit drop groups exist and are attached to the six maps. It can run again without changes.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public static void EnsureFor(IContext context, GameConfiguration gameConfiguration)
    {
        var fruit = gameConfiguration.Items.FirstOrDefault(item => item.Group == FruitItemGroup && item.Number == FruitItemNumber);
        if (fruit is null)
        {
            return;
        }

        var groups = Enumerable.Range(0, FruitNames.Count)
            .Select(level => GetOrCreateGroup(context, gameConfiguration, fruit, (byte)level))
            .ToList();

        foreach (var map in gameConfiguration.Maps.Where(m => MapNumbers.Contains(m.Number) && m.Discriminator == 0))
        {
            foreach (var group in groups.Where(g => !map.DropItemGroups.Any(existing => existing.GetId() == g.GetId())))
            {
                map.DropItemGroups.Add(group);
            }
        }
    }

    private static DropItemGroup GetOrCreateGroup(IContext context, GameConfiguration gameConfiguration, ItemDefinition fruit, byte level)
    {
        var id = GuidHelper.CreateGuid<DropItemGroup>(GuidParentNumber, level);
        var group = gameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == id);
        if (group is null)
        {
            group = context.CreateNew<DropItemGroup>();
            group.SetGuid(GuidParentNumber, level);
            gameConfiguration.DropItemGroups.Add(group);
        }

        group.Description = $"Fruit of {FruitNames[level]} ({ChancePerFruit * 100:0.##} % drop chance)";
        group.Chance = ChancePerFruit;
        group.ItemLevel = level;
        group.MinimumMonsterLevel = null;
        group.MaximumMonsterLevel = null;
        if (group.PossibleItems.Count != 1 || group.PossibleItems.First().GetItemId() != fruit.GetItemId())
        {
            group.PossibleItems.Clear();
            group.PossibleItems.Add(fruit);
        }

        return group;
    }
}
