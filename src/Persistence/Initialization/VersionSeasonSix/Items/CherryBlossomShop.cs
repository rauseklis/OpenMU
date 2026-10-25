// <copyright file="CherryBlossomShop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;

/// <summary>
/// Replaces Lumen's BC/DS ticket materials with full branch stacks for exchange testing.
/// </summary>
public sealed class CherryBlossomShop(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    private const short LumenNumber = 255;
    private const int TestPrice = 1000;
    private const int ShopSlots = 120;

    /// <inheritdoc />
    public override void Initialize()
    {
        var store = this.GameConfiguration.Monsters.Single(monster => monster.Number == LumenNumber).MerchantStore
            ?? throw new InvalidOperationException("Lumen has no merchant store.");
        foreach (var material in store.Items.Where(item => item.Definition is { Group: 13, Number: 16 or 17 }
            or { Group: 14, Number: 17 or 18 }).ToArray())
        {
            store.Items.Remove(material);
        }

        foreach (var (number, count) in new (short Number, byte Count)[] { (88, 10), (89, 30), (90, 255) })
        {
            this.EnsureListing(store, number, count);
        }
    }

    private void EnsureListing(ItemStorage store, short number, byte count)
    {
        var definition = this.GameConfiguration.Items.Single(item => item.Group == 14 && item.Number == number);
        if (store.Items.Any(item => item.Definition == definition))
        {
            return;
        }

        var listing = this.Context.CreateNew<Item>();
        listing.Definition = definition;
        listing.Durability = count;
        listing.StorePrice = TestPrice;
        listing.ItemSlot = new Storage(ShopSlots, store).CheckInvSpace(listing)
            ?? throw new InvalidOperationException("Lumen's shop has no space for a branch stack.");
        store.Items.Add(listing);
    }
}
