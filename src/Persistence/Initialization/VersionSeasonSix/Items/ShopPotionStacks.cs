// <copyright file="ShopPotionStacks.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Makes every merchant sell each potion only at level +0 and in exactly three amounts: 1, 10 and 25 pieces.
/// </summary>
public static class ShopPotionStacks
{
    /// <summary>
    /// The amounts in which a shop sells every potion.
    /// </summary>
    public static readonly byte[] StackSizes = [1, 10, 25];

    /// <summary>
    /// The number of potions which fit on one inventory slot (the item definition's durability).
    /// </summary>
    public const byte MaximumStackSize = 50;

    private const int StoreWidth = 8;
    private const int StoreHeight = 15;

    /// <summary>
    /// The numbers (item group 14) of all potion types which are sold in stacks.
    /// </summary>
    private static readonly byte[] PotionNumbers =
    [
        0,                  // Apple
        1, 2, 3,            // Small, Medium, Large Healing Potion
        4, 5, 6,            // Small, Medium, Large Mana Potion
        8,                  // Antidote
        35, 36, 37,         // Small, Medium, Large Shield Potion
        38, 39, 40,         // Small, Medium, Large Complex Potion
    ];

    /// <summary>
    /// Determines whether the definition is a stackable potion type.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <returns><see langword="true"/> if it is a potion.</returns>
    public static bool IsPotion(ItemDefinition definition) => definition.Group == 14 && PotionNumbers.Contains((byte)definition.Number);

    /// <summary>
    /// Sets the stack limit of all potion definitions to <see cref="MaximumStackSize"/>.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    public static void SetMaximumStackSize(GameConfiguration configuration)
    {
        foreach (var definition in configuration.Items.Where(IsPotion))
        {
            definition.Durability = MaximumStackSize;
        }
    }

    /// <summary>
    /// Removes all potion listings from the store and returns the potion types which were sold.
    /// </summary>
    /// <param name="store">The merchant store.</param>
    /// <param name="removed">The removed listings, which the caller has to delete from the database.</param>
    /// <returns>The potion numbers which were sold, each once.</returns>
    public static IReadOnlyList<byte> RemovePotions(ItemStorage store, out IReadOnlyList<Item> removed)
    {
        var listings = store.Items.Where(item => item.Definition is { } definition && IsPotion(definition)).ToList();
        foreach (var listing in listings)
        {
            store.Items.Remove(listing);
        }

        removed = listings;
        return PotionNumbers.Where(number => listings.Any(listing => listing.Definition!.Number == number)).ToList();
    }

    /// <summary>
    /// Adds the three listings (1, 10 and 25 pieces, level +0) of each given potion type into free slots of the store.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="store">The merchant store.</param>
    /// <param name="potionNumbers">The potion types to sell.</param>
    public static void AddListings(IContext context, GameConfiguration configuration, ItemStorage store, IEnumerable<byte> potionNumbers)
    {
        var occupied = new bool[StoreWidth * StoreHeight];
        foreach (var item in store.Items)
        {
            Occupy(occupied, item.ItemSlot, item.Definition?.Width ?? 1, item.Definition?.Height ?? 1);
        }

        foreach (var number in potionNumbers.Distinct())
        {
            var definition = configuration.Items.Single(item => item.Group == 14 && item.Number == number);
            foreach (var stackSize in StackSizes)
            {
                var slot = Array.IndexOf(occupied, false);
                if (slot < 0)
                {
                    throw new InvalidOperationException("The merchant store has no free slot left for the potions.");
                }

                var listing = context.CreateNew<Item>();
                listing.Definition = definition;
                listing.ItemSlot = (byte)slot;
                listing.Durability = stackSize;
                listing.Level = 0;
                store.Items.Add(listing);
                occupied[slot] = true;
            }
        }
    }

    private static void Occupy(bool[] occupied, int slot, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = slot + (y * StoreWidth) + x;
                if (index < occupied.Length)
                {
                    occupied[index] = true;
                }
            }
        }
    }
}
