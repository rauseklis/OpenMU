// <copyright file="InfernoGoldenMonstersUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Boosts the Golden monsters on server 1 only: defense, attack rate, defense rate and health ten times the base value, damage doubled.
/// The percentages of the other monsters and of the other server are kept.
/// </summary>
[PlugIn]
[Guid("1475789E-F314-4EDC-9CC0-4F98EE3AF3FA")]
public sealed class InfernoGoldenMonstersUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The monster numbers of the Golden monsters (without the Golden Compensation Box).</summary>
    internal const string GoldenMonsterNumbers = "43,53,54,78,79,80,81,82,83";

    /// <summary>The increase in percent which means ten times the base value.</summary>
    internal const float TimesTen = 900f;

    /// <summary>The increase in percent which means double the base value.</summary>
    internal const float Double = 100f;

    /// <inheritdoc />
    public override string Name => "Golden monsters x10 on server 1";

    /// <inheritdoc />
    public override string Description => "On server 1 (internal id 0) the Golden monsters get defense, attack rate, defense rate and health x10 and damage x2 (from the base values). Other monsters and the other server are not changed.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.InfernoGoldenMonsters;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var typeId = typeof(MonsterAttributeScaler).GUID;
        var plugInConfiguration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeId);
        if (plugInConfiguration is null)
        {
            // The scaling of server 1 is installed by an earlier update; without it there is nothing to boost.
            return ValueTask.CompletedTask;
        }

        var scalerConfiguration = plugInConfiguration.GetConfiguration<MonsterAttributeScalerConfiguration>(null);
        if (scalerConfiguration is null)
        {
            return ValueTask.CompletedTask;
        }

        scalerConfiguration.BoostedMonsterNumbers = GoldenMonsterNumbers;
        scalerConfiguration.BoostedDamagePercentage = Double;
        scalerConfiguration.BoostedAttackRatePercentage = TimesTen;
        scalerConfiguration.BoostedDefenseRatePercentage = TimesTen;
        scalerConfiguration.BoostedDefensePercentage = TimesTen;
        scalerConfiguration.BoostedHealthPercentage = TimesTen;
        plugInConfiguration.SetConfiguration(scalerConfiguration, null);
        return ValueTask.CompletedTask;
    }
}
