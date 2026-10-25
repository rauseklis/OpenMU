// <copyright file="ReusableEventTicketShop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Adds configurable reusable tickets to Lumen without replacing existing stock or later edits.
/// </summary>
public sealed class ReusableEventTicketShop : InitializerBase
{
    private const short LumenNumber = 255;
    private const int TestPrice = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReusableEventTicketShop"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    public ReusableEventTicketShop(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var store = this.GameConfiguration.Monsters.Single(monster => monster.Number == LumenNumber).MerchantStore
            ?? throw new InvalidOperationException("Lumen has no merchant store.");
        foreach (var number in ReusableEventTicketItems.TicketNumbers)
        {
            var definition = this.GameConfiguration.Items.Single(item => item.Group == ReusableEventTicketItems.TicketGroup && item.Number == number);
            if (store.Items.Any(item => item.Definition == definition))
            {
                continue;
            }

            var listing = this.Context.CreateNew<Item>();
            listing.Definition = definition;
            listing.Durability = ReusableEventTicketItems.DefaultUses;
            listing.StorePrice = TestPrice;
            listing.ItemSlot = LumenBoxListings.FindFreeSlot(store);
            store.Items.Add(listing);
        }
    }

}
