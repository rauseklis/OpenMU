// <copyright file="LorenciaBarCustomItems.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Adds the custom items (the Invisible set and the Mace of King) to the shop of Lumen the Barmaid in the Lorencia bar,
/// at level +0 and without a fixed store price (the price is calculated from the item like for every other item),
/// in two aligned blocks below the existing stock. Items which are already listed are left alone.
/// </summary>
public sealed class LorenciaBarCustomItems : InitializerBase
{
    private const short LumenNpcNumber = 255;
    private const int ShopWidth = 8;
    private const int ShopHeight = 15;

    /// <summary>
    /// The listed items: group, number and the preferred slot.
    /// Bottom rows 13-15: Invisible Armor (2x3), Mace of King (1x3), Invisible Helm and Pants (2x2).
    /// Rows 3-4, next to the existing stock: Invisible Gloves and Boots (2x2). Rows 5 and 6 stay empty.
    /// </summary>
    internal static readonly (byte Group, short Number, byte Slot)[] Listings =
    [
        (8, InvisibleArmors.Number, 96),    // Invisible Armor
        (2, 22, 98),                        // Mace of King
        (7, InvisibleArmors.Number, 99),    // Invisible Helm
        (9, InvisibleArmors.Number, 101),   // Invisible Pants
        (10, InvisibleArmors.Number, 19),   // Invisible Gloves
        (11, InvisibleArmors.Number, 21),   // Invisible Boots
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="LorenciaBarCustomItems"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    public LorenciaBarCustomItems(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var store = this.GameConfiguration.Monsters.Single(monster => monster.Number == LumenNpcNumber).MerchantStore
            ?? throw new InvalidOperationException("Lumen the Barmaid has no merchant store.");
        foreach (var (group, number, slot) in Listings)
        {
            var definition = this.GameConfiguration.Items.Single(item => item.Group == group && item.Number == number);
            if (store.Items.Any(item => item.Definition == definition))
            {
                continue;
            }

            var itemSlot = IsFree(store, slot, definition.Width, definition.Height) ? slot : FindFreeSlot(store, definition.Width, definition.Height);
            var listing = this.Context.CreateNew<Item>();
            listing.Definition = definition;
            listing.ItemSlot = itemSlot;
            listing.Durability = definition.Durability;
            listing.Level = 0;
            store.Items.Add(listing);
        }
    }

    private static bool IsFree(ItemStorage store, int slot, int width, int height)
    {
        if ((slot % ShopWidth) + width > ShopWidth || (slot / ShopWidth) + height > ShopHeight)
        {
            return false;
        }

        var occupied = new bool[ShopWidth * ShopHeight];
        foreach (var item in store.Items)
        {
            var itemDefinition = item.Definition!;
            for (var y = 0; y < itemDefinition.Height; y++)
            {
                for (var x = 0; x < itemDefinition.Width; x++)
                {
                    var index = item.ItemSlot + (y * ShopWidth) + x;
                    if (index < occupied.Length)
                    {
                        occupied[index] = true;
                    }
                }
            }
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (occupied[slot + (y * ShopWidth) + x])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static byte FindFreeSlot(ItemStorage store, int width, int height)
    {
        // Rows 5 and 6 of the shop are kept empty on purpose; the search starts below the existing stock.
        foreach (var slot in Enumerable.Range(8 * ShopWidth, 7 * ShopWidth).Concat(Enumerable.Range(2 * ShopWidth, 2 * ShopWidth)))
        {
            if (IsFree(store, slot, width, height))
            {
                return (byte)slot;
            }
        }

        throw new InvalidOperationException("Lumen's shop has no free space for another item.");
    }
}
