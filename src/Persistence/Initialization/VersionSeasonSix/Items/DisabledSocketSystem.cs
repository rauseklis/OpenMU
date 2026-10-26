// <copyright file="DisabledSocketSystem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;

/// <summary>
/// Retains socket definitions for legacy-item loading while disabling every socket capability and recipe.
/// </summary>
public sealed class DisabledSocketSystem : InitializerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisabledSocketSystem"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The selected configuration.</param>
    public DisabledSocketSystem(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        foreach (var item in this.GameConfiguration.Items)
        {
            item.MaximumSockets = 0;
            foreach (var options in item.PossibleItemOptions.Where(options => options.PossibleOptions.Any()
                && options.PossibleOptions.All(option => SocketOptionPolicy.IsSocketOption(option.OptionType))).ToArray())
            {
                item.PossibleItemOptions.Remove(options);
            }
        }

        foreach (var bonus in this.GameConfiguration.ItemOptionCombinationBonuses
            .Where(bonus => bonus.Requirements.Any(requirement => SocketOptionPolicy.IsSocketOption(requirement.OptionType))).ToArray())
        {
            this.GameConfiguration.ItemOptionCombinationBonuses.Remove(bonus);
        }

        foreach (var npc in this.GameConfiguration.Monsters.Where(monster => monster.NpcWindow is NpcWindow.SeedMaster or NpcWindow.SeedResearcher))
        {
            npc.ItemCraftings.Clear();
        }
    }
}
