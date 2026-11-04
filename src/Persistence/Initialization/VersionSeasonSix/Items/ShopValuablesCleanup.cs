// <copyright file="ShopValuablesCleanup.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Removes jewels, boxes and other valuables from every merchant store.
/// </summary>
public sealed class ShopValuablesCleanup(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    private static readonly HashSet<(byte Group, short Number)> Valuables =
    [
        (12, 15),                                                                   // Jewel of Chaos
        (12, 30), (12, 31), (12, 136), (12, 137), (12, 138), (12, 139), (12, 140), // Packed jewels
        (12, 141), (12, 142), (12, 143),                                            // Packed jewels and refine stones
        (12, 32), (12, 33), (12, 34),                                               // Red, Green and Blue Ribbon Box
        (14, 11),                                                                   // Box of Luck, all levels (medals, Box of Kundun)
        (14, 13), (14, 14), (14, 16), (14, 22), (14, 31), (14, 41), (14, 42),       // Jewels and Gemstone
        (14, 43), (14, 44),                                                         // Lower and Higher refine stone
        (14, 32), (14, 33), (14, 34),                                               // Pink, Red and Blue Chocolate Box
        (14, 52),                                                                   // GM Gift
        (14, 84),                                                                   // Cherry Blossom Play-Box
        (14, 123), (14, 124),                                                       // Golden and Silver Box
    ];

    /// <summary>
    /// Determines whether the item is a jewel, box or similar valuable that no shop should sell.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <returns><see langword="true"/> if shops must not sell the item.</returns>
    public static bool IsValuable(ItemDefinition definition) => Valuables.Contains((definition.Group, definition.Number));

    /// <summary>
    /// Removes the valuables from all merchant stores.
    /// </summary>
    /// <returns>The removed store listings.</returns>
    public IReadOnlyList<Item> RemoveFromAllStores()
    {
        var removed = new List<Item>();
        foreach (var store in this.GameConfiguration.Monsters.Select(monster => monster.MerchantStore).OfType<ItemStorage>())
        {
            foreach (var listing in store.Items.Where(item => item.Definition is { } definition && IsValuable(definition)).ToList())
            {
                store.Items.Remove(listing);
                removed.Add(listing);
            }
        }

        return removed;
    }

    /// <inheritdoc />
    public override void Initialize() => this.RemoveFromAllStores();
}
