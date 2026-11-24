// <copyright file="ShopPotionStacksUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>Lets potions stack to 50 and makes every merchant sell them at level +0 in amounts of 1, 10 and 25.</summary>
[PlugIn]
[Guid("5A0C1E7B-3D52-4B6F-9E1A-7C2D84B0F613")]
public sealed class ShopPotionStacksUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Potion stacks of 50 and shop amounts 1, 10, 25";

    /// <inheritdoc />
    public override string Description => "Potions stack up to 50 per slot; every merchant sells each potion only at level +0 as 1, 10 and 25 pieces.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ShopPotionStacks;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        ShopPotionStacks.SetMaximumStackSize(gameConfiguration);
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
