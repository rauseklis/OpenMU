// <copyright file="ConfigureDevilSquareFinalRewardsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Applies the final DS EXP table and removes completion items while preserving Zen and BC pools.
/// </summary>
[PlugIn]
[Guid("F45F8BCC-429C-4F32-923B-5F98CFE0CC66")]
public sealed class ConfigureDevilSquareFinalRewardsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Devil Square Zen and EXP only";

    /// <inheritdoc />
    public override string Description => "Applies the final Devil Square EXP table and removes completion items, preserving Zen and Blood Castle rewards.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.DevilSquareZenAndExperienceOnly;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 23, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new DevilSquareExperienceRewards(context, gameConfiguration).Initialize();
        foreach (var square in gameConfiguration.MiniGameDefinitions.Where(game => game.Type == MiniGameType.DevilSquare))
        {
            foreach (var item in square.Rewards.Where(reward => reward.RewardType is MiniGameRewardType.Item or MiniGameRewardType.ItemDrop).ToArray())
            {
                square.Rewards.Remove(item);
            }
        }

        return ValueTask.CompletedTask;
    }
}
