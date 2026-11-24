// <copyright file="HealthLossPerHit.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Makes the Imp (Satan), the Demon and the wings of the 1st, 2nd and 3rd class take health from the wearer
/// with every hit on a monster or player (<see cref="Stats.HealthLossAfterHit"/>).
/// </summary>
/// <remarks>
/// The same values are used for new databases (at the end of the initialization) and for the existing database
/// (<see cref="Updates.HealthLossPerHitUpdatePlugIn"/>, installed once). Both game servers (x5 and x500) share
/// this configuration. The server never takes the last health point, so the loss cannot kill the player.
/// </remarks>
public sealed class HealthLossPerHit(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    /// <summary>
    /// The items (item group, item number, health lost per hit).
    /// Capes and the wings of the 4th class and the premium wings are not part of it.
    /// </summary>
    public static readonly IReadOnlyList<(byte Group, short Number, float Health)> Items =
    [
        (13, 1, 3f), // Imp (Satan)
        (13, 64, 4f), // Demon

        (12, 0, 1f), // Wings of Elf
        (12, 1, 1f), // Wings of Heaven
        (12, 2, 1f), // Wings of Satan
        (12, 41, 1f), // Wings of Curse

        (12, 3, 3f), // Wings of Spirits
        (12, 4, 3f), // Wings of Soul
        (12, 5, 3f), // Wings of Dragon
        (12, 6, 3f), // Wings of Darkness
        (12, 42, 3f), // Wings of Despair

        (12, 36, 5f), // Wing of Storm
        (12, 37, 5f), // Wing of Eternal
        (12, 38, 5f), // Wing of Illusion
        (12, 39, 5f), // Wing of Ruin
        (12, 43, 5f), // Wing of Dimension
    ];

    /// <inheritdoc />
    public override void Initialize()
    {
        var attribute = Stats.HealthLossAfterHit.GetPersistent(this.GameConfiguration);
        foreach (var (group, number, health) in Items)
        {
            var item = this.GameConfiguration.Items.SingleOrDefault(i => i.Group == group && i.Number == number);
            if (item is null)
            {
                continue;
            }

            var powerUp = item.BasePowerUpAttributes.FirstOrDefault(p => p.TargetAttribute?.Id == Stats.HealthLossAfterHit.Id);
            if (powerUp is null)
            {
                powerUp = this.Context.CreateNew<ItemBasePowerUpDefinition>();
                powerUp.TargetAttribute = attribute;
                powerUp.AggregateType = AggregateType.AddRaw;
                item.BasePowerUpAttributes.Add(powerUp);
            }

            powerUp.BaseValue = health;
        }
    }
}
