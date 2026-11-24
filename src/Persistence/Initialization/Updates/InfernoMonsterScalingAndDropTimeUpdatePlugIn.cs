// <copyright file="InfernoMonsterScalingAndDropTimeUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sets the duration of dropped items to 30 seconds (all servers) and switches on the monster scaling
/// for server 1 only (+50% attack rate, +20% health, nothing else).
/// </summary>
[PlugIn]
[Guid("90F8F746-A268-4D81-83B3-2E1A1F0D3780")]
public sealed class InfernoMonsterScalingAndDropTimeUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The duration of dropped items in seconds.</summary>
    internal const int ItemDropSeconds = 30;

    /// <summary>The internal id of the only server which gets the monster scaling.</summary>
    internal const byte ScaledServerId = 0;

    /// <summary>The attack rate increase in percent.</summary>
    internal const float AttackRatePercent = 50f;

    /// <summary>The health increase in percent.</summary>
    internal const float HealthPercent = 20f;

    /// <inheritdoc />
    public override string Name => "Item drop time 30 s, monster scaling on server 1";

    /// <inheritdoc />
    public override string Description => "Dropped items disappear after 30 seconds. Server 1 (internal id 0) gets monsters with +50% attack rate and +20% health; damage, defense and defense rate are not changed.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.InfernoMonsterScalingAndDropTime;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        gameConfiguration.ItemDropDuration = TimeSpan.FromSeconds(ItemDropSeconds);

        var typeId = typeof(MonsterAttributeScaler).GUID;
        var plugInConfiguration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeId);
        if (plugInConfiguration is null)
        {
            plugInConfiguration = context.CreateNew<PlugInConfiguration>();
            plugInConfiguration.SetGuid(typeId);
            plugInConfiguration.TypeId = typeId;
            gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
        }

        var scalerConfiguration = new MonsterAttributeScalerConfiguration
        {
            ScaleAllPercentage = 0f,
            DamagePercentage = 0f,
            AttackRatePercentage = AttackRatePercent,
            DefenseRatePercentage = 0f,
            DefensePercentage = 0f,
            HealthPercentage = HealthPercent,
            OnlyServerId = ScaledServerId,
        };

        plugInConfiguration.SetConfiguration(scalerConfiguration, null);
        plugInConfiguration.IsActive = true;
        return ValueTask.CompletedTask;
    }
}
