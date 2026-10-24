// <copyright file="AncientBoxShop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Adds the three Ancient reward boxes to Lumen's shop without replacing existing stock.
/// </summary>
public sealed class AncientBoxShop : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AncientBoxShop"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    public AncientBoxShop(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize() => new LumenBoxListings(this.Context, this.GameConfiguration, 14, 11, 124, 123).Initialize();
}
