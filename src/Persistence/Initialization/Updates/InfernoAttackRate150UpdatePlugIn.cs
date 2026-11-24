// <copyright file="InfernoAttackRate150UpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Raises the monster attack rate increase (server 1 only) from 50% to 150%. The other scaler values are kept.
/// </summary>
[PlugIn]
[Guid("A5D99C21-BF66-4C4A-8D58-7F4ADAC575F4")]
public sealed class InfernoAttackRate150UpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The attack rate increase in percent.</summary>
    internal const float AttackRatePercent = 150f;

    /// <inheritdoc />
    public override string Name => "Monster attack rate +150% on server 1";

    /// <inheritdoc />
    public override string Description => "Raises the monster attack rate increase of server 1 (internal id 0) from 50% to 150%. Other values stay as they are.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.InfernoAttackRate150;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var typeId = typeof(MonsterAttributeScaler).GUID;
        var plugInConfiguration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeId);
        if (plugInConfiguration is null)
        {
            plugInConfiguration = context.CreateNew<PlugInConfiguration>();
            plugInConfiguration.SetGuid(typeId);
            plugInConfiguration.TypeId = typeId;
            gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
        }

        var scalerConfiguration = plugInConfiguration.GetConfiguration<MonsterAttributeScalerConfiguration>(null)
            ?? new MonsterAttributeScalerConfiguration
            {
                DamagePercentage = 0f,
                DefenseRatePercentage = 0f,
                DefensePercentage = 0f,
                HealthPercentage = InfernoMonsterScalingAndDropTimeUpdatePlugIn.HealthPercent,
                OnlyServerId = InfernoMonsterScalingAndDropTimeUpdatePlugIn.ScaledServerId,
            };

        scalerConfiguration.AttackRatePercentage = AttackRatePercent;
        plugInConfiguration.SetConfiguration(scalerConfiguration, null);
        plugInConfiguration.IsActive = true;
        return ValueTask.CompletedTask;
    }
}
