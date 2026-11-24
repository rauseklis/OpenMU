// <copyright file="ShopPotionLayoutUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>Lays the shop potions out in aligned rows: one column per potion type, rows of 1, 10 and 25.</summary>
[PlugIn]
[Guid("B7D41E92-6C0A-4F35-8A1D-2E9F5B3C7D08")]
public sealed class ShopPotionLayoutUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Aligned potion shop layout";

    /// <inheritdoc />
    public override string Description => "Re-lays the potions of every merchant in clean rows (one column per potion type; row 1: single, row 2: stacks of 10, row 3: stacks of 25). Amounts, levels and prices are unchanged.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ShopPotionLayout;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var store in gameConfiguration.Monsters.Select(monster => monster.MerchantStore).OfType<ItemStorage>().Distinct())
        {
            var sold = ShopPotionStacks.RemovePotions(store, out var removed);
            foreach (var listing in removed)
            {
                await context.DeleteAsync(listing).ConfigureAwait(false);
            }

            ShopPotionStacks.AddListings(context, gameConfiguration, store, sold);
        }
    }
}
