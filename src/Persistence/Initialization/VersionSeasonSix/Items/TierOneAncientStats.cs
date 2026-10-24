// <copyright file="TierOneAncientStats.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Adds the approved full-set stats to low-tier Ancient equipment.
/// </summary>
public sealed class TierOneAncientStats : InitializerBase
{
    private const float StandardBonus = 100;
    private const float BrassBonus = 75;

    /// <summary>
    /// Initializes a new instance of the <see cref="TierOneAncientStats"/> class.
    /// </summary>
    public TierOneAncientStats(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Hyperion", StandardBonus);
        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Barnake", StandardBonus);
        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Fase", StandardBonus);
        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Cloud", BrassBonus);
    }
}
