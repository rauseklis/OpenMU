// <copyright file="ChocolateBonusStock.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Adds missing reviewed bonus boxes to Lumen and leaves Red/Blue Chocolate openings empty.
/// </summary>
public sealed class ChocolateBonusStock : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChocolateBonusStock"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The selected configuration.</param>
    public ChocolateBonusStock(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var store = this.GameConfiguration.Monsters.Single(monster => monster.Number == 255).MerchantStore
            ?? throw new InvalidOperationException("Lumen has no merchant store.");
        foreach (var level in new byte[] { 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12 })
        {
            this.EnsureListing(store, 11, level);
        }

        foreach (var number in new short[] { 32, 33, 34, 84 })
        {
            this.EnsureListing(store, number, 0);
        }

        foreach (var number in new short[] { 33, 34 })
        {
            var box = this.GameConfiguration.Items.Single(item => item.Group == 14 && item.Number == number);
            box.DropItems.Clear();
            // Keep a real opening group: an empty DropItems collection would drop the box itself.
            var empty = this.Context.CreateNew<ItemDropItemGroup>();
            empty.Description = $"{box.Name} - intentionally empty";
            empty.SourceItemLevel = 0;
            empty.Chance = 1;
            empty.ItemType = SpecialItemType.RandomItem;
            box.DropItems.Add(empty);
        }
    }

    private void EnsureListing(ItemStorage store, short number, byte level)
    {
        var definition = this.GameConfiguration.Items.Single(item => item.Group == 14 && item.Number == number);
        if (store.Items.Any(item => item.Definition == definition && item.Level == level))
        {
            return;
        }

        var listing = this.Context.CreateNew<Item>();
        listing.Definition = definition;
        listing.Level = level;
        listing.Durability = 1;
        listing.StorePrice = 1000;
        listing.ItemSlot = LumenBoxListings.FindFreeSlot(store);
        store.Items.Add(listing);
    }
}
