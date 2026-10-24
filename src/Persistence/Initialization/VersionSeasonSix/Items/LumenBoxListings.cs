// <copyright file="LumenBoxListings.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Adds selected reward boxes to Lumen's shop without replacing existing stock.
/// </summary>
public sealed class LumenBoxListings : InitializerBase
{
    private const short LumenNpcNumber = 255;
    private const int BoxPrice = 1000;
    private const int ShopWidth = 8;
    private const int ShopHeight = 15;

    private readonly byte _boxGroup;
    private readonly short[] _boxNumbers;

    /// <summary>
    /// Initializes a new instance of the <see cref="LumenBoxListings"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="boxGroup">The item group.</param>
    /// <param name="boxNumbers">The item numbers.</param>
    public LumenBoxListings(IContext context, GameConfiguration configuration, byte boxGroup, params short[] boxNumbers)
        : base(context, configuration)
    {
        this._boxGroup = boxGroup;
        this._boxNumbers = boxNumbers;
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var store = this.GameConfiguration.Monsters.Single(monster => monster.Number == LumenNpcNumber).MerchantStore
            ?? throw new InvalidOperationException("Lumen the Barmaid has no merchant store.");
        foreach (var number in this._boxNumbers)
        {
            this.EnsureListing(store, number);
        }
    }

    private void EnsureListing(ItemStorage store, short number)
    {
        var definition = this.GameConfiguration.Items.Single(item => item.Group == this._boxGroup && item.Number == number);
        var existing = store.Items.Where(item => item.Definition == definition && item.Level == 0).ToList();
        if (existing.Count > 0)
        {
            foreach (var item in existing)
            {
                item.StorePrice = BoxPrice;
            }

            return;
        }

        var itemSlot = FindFreeSlot(store);
        var listing = this.Context.CreateNew<Item>();
        listing.Definition = definition;
        listing.ItemSlot = itemSlot;
        listing.Durability = 1;
        listing.StorePrice = BoxPrice;
        store.Items.Add(listing);
    }

    private static byte FindFreeSlot(ItemStorage store)
    {
        var occupied = new bool[ShopWidth * ShopHeight];
        foreach (var item in store.Items)
        {
            var definition = item.Definition!;
            for (var row = 0; row < definition.Height; row++)
            {
                for (var column = 0; column < definition.Width; column++)
                {
                    var slot = item.ItemSlot + (row * ShopWidth) + column;
                    if (slot < occupied.Length)
                    {
                        occupied[slot] = true;
                    }
                }
            }
        }

        var freeSlot = Array.FindIndex(occupied, used => !used);
        return freeSlot >= 0 ? (byte)freeSlot : throw new InvalidOperationException("Lumen's shop has no free space for a reward box.");
    }
}
