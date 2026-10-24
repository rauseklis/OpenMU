// <copyright file="RibbonBoxShop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Adds the three ribbon boxes to Lumen's shop without replacing existing stock.
/// </summary>
public sealed class RibbonBoxShop : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RibbonBoxShop"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    public RibbonBoxShop(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize() => new LumenBoxListings(this.Context, this.GameConfiguration, 12, 32, 33, 34).Initialize();
}
