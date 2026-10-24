// <copyright file="TierTwoAncientStats.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Adds the approved full-set stats to mid-tier Ancient equipment.
/// </summary>
public sealed class TierTwoAncientStats : InitializerBase
{
    private const float StandardBonus = 50;

    /// <summary>
    /// Initializes a new instance of the <see cref="TierTwoAncientStats"/> class.
    /// </summary>
    public TierTwoAncientStats(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Garuda", StandardBonus);
        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Heras", StandardBonus);
        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Gaia", StandardBonus);
        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Eplete", StandardBonus);
        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Semeden", StandardBonus);
        AncientFullSetStats.AddFullSetStats(this.Context, this.GameConfiguration, "Chamer", StandardBonus);
    }
}
