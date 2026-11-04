// <copyright file="RemoveShopValuablesUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>Removes jewels, boxes and other valuables from every merchant store.</summary>
[PlugIn]
[Guid("12219951-1F29-408B-8B0F-64B4625662FD")]
public sealed class RemoveShopValuablesUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Remove valuables from shops";

    /// <inheritdoc />
    public override string Description => "Removes jewels, boxes, chocolate boxes and similar valuables from every merchant store.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.RemoveShopValuables;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 4, 8, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var listing in new ShopValuablesCleanup(context, gameConfiguration).RemoveFromAllStores())
        {
            await context.DeleteAsync(listing).ConfigureAwait(false);
        }
    }
}
