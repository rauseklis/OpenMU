// <copyright file="ChaosItemUpgradeRatesUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>Updates only the six approved item upgrade recipes in existing Season Six configurations.</summary>
[PlugIn]
[Guid("D6409CB6-8461-4470-A294-3F6CBC92B911")]
public sealed class ChaosItemUpgradeRatesUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Install approved item upgrade rates and Luck bonus";

    /// <inheritdoc />
    public override string Description => "Sets +10 through +15 bases to 60/60/55/55/20/20 percent, Luck to +20, and Excellent/Ancient to -10; preserves other crafting settings.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ChaosItemUpgradeRates;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 29, 20, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        byte[] mixNumbers = [3, 4, 22, 23, 49, 50];
        var goblin = gameConfiguration.Monsters.Single(monster => monster.Number == 238);
        for (var index = 0; index < mixNumbers.Length; index++)
        {
            var settings = goblin.ItemCraftings.Single(crafting => crafting.Number == mixNumbers[index]).SimpleCraftingSettings
                ?? throw new InvalidOperationException("An item upgrade recipe has no crafting settings.");
            settings.SuccessPercent = ChaosItemUpgradeRates.GetBaseRate((byte)(10 + index));
            settings.SuccessPercentageAdditionForLuck = ChaosItemUpgradeRates.LuckBonus;
            settings.SuccessPercentageAdditionForExcellentItem = -10;
            settings.SuccessPercentageAdditionForAncientItem = -10;
        }

        return ValueTask.CompletedTask;
    }
}
