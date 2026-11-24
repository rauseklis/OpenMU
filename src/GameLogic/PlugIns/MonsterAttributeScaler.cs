// <copyright file="MonsterAttributeScaler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A feature plugin which scales all monster base stats by a configurable percentage.
/// Uses shared multiplicative IElement instances. Configuration changes take effect immediately.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.MonsterAttributeScaler_Name), Description = nameof(PlugInResources.MonsterAttributeScaler_Description), ResourceType = typeof(PlugInResources))]
[Guid("59E5B45F-0A3D-4BAF-8B6C-9E1D2F3A4B5C")]
public class MonsterAttributeScaler : IFeaturePlugIn, IObjectAddedToMapPlugIn, IObjectRemovedFromMapPlugIn,
    ISupportCustomConfiguration<MonsterAttributeScalerConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private readonly Multipliers _main = new();
    private readonly Multipliers _other = new();

    private MonsterAttributeScalerConfiguration? _configuration;

    /// <inheritdoc />
    public MonsterAttributeScalerConfiguration? Configuration
    {
        get => this._configuration;
        set
        {
            this._configuration = value;

            if (value is null)
            {
                this._main.Set(0, 0, 0, 0, 0);
                this._other.Set(0, 0, 0, 0, 0);
                return;
            }

            this._main.Set(value.DamagePercentage, value.AttackRatePercentage, value.DefenseRatePercentage, value.DefensePercentage, value.HealthPercentage);

            // The other server only has its own attack rate and health; the other values are the same as the main ones.
            this._other.Set(value.DamagePercentage, value.OtherServerAttackRatePercentage, value.DefenseRatePercentage, value.DefensePercentage, value.OtherServerHealthPercentage);
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new MonsterAttributeScalerConfiguration();
    }

    /// <inheritdoc />
    public ValueTask ObjectAddedToMapAsync(GameMap map, ILocateable addedObject)
    {
        if (addedObject is not AttackableNpcBase monster || this.GetMultipliers(map) is not { } multipliers)
        {
            return ValueTask.CompletedTask;
        }

        monster.Attributes.AddElement(multipliers.Damage, Stats.MinimumPhysBaseDmg);
        monster.Attributes.AddElement(multipliers.Damage, Stats.MaximumPhysBaseDmg);
        monster.Attributes.AddElement(multipliers.AttackRate, Stats.AttackRatePvm);
        monster.Attributes.AddElement(multipliers.DefenseRate, Stats.DefenseRatePvm);
        monster.Attributes.AddElement(multipliers.Defense, Stats.DefenseBase);
        monster.Attributes.AddElement(multipliers.Health, Stats.MaximumHealth);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ObjectRemovedFromMapAsync(GameMap map, ILocateable removedObject)
    {
        if (removedObject is not AttackableNpcBase monster || this.GetMultipliers(map) is not { } multipliers)
        {
            return ValueTask.CompletedTask;
        }

        monster.Attributes.RemoveElement(multipliers.Damage, Stats.MinimumPhysBaseDmg);
        monster.Attributes.RemoveElement(multipliers.Damage, Stats.MaximumPhysBaseDmg);
        monster.Attributes.RemoveElement(multipliers.AttackRate, Stats.AttackRatePvm);
        monster.Attributes.RemoveElement(multipliers.DefenseRate, Stats.DefenseRatePvm);
        monster.Attributes.RemoveElement(multipliers.Defense, Stats.DefenseBase);
        monster.Attributes.RemoveElement(multipliers.Health, Stats.MaximumHealth);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the attack rate and health factor which are applied to the monsters of the specified game server.
    /// </summary>
    /// <param name="serverId">The internal id of the game server.</param>
    /// <returns>The factors; 1 means no scaling.</returns>
    internal (float AttackRate, float Health) GetFactors(byte? serverId)
    {
        var multipliers = this.GetMultipliers(serverId);
        return multipliers is null ? (1.0f, 1.0f) : (multipliers.AttackRate.Value, multipliers.Health.Value);
    }

    private Multipliers? GetMultipliers(GameMap map) => this.GetMultipliers(map.ServerId);

    private Multipliers? GetMultipliers(byte? serverId)
    {
        var configuration = this._configuration;
        if (configuration is null)
        {
            return null;
        }

        if (configuration.OtherServerId is { } otherServerId && serverId == otherServerId)
        {
            return this._other;
        }

        var onlyServerId = configuration.OnlyServerId;
        return onlyServerId is null || serverId == onlyServerId ? this._main : null;
    }

    private sealed class Multipliers
    {
        public SimpleElement Damage { get; } = new(1.0f, AggregateType.Multiplicate);

        public SimpleElement AttackRate { get; } = new(1.0f, AggregateType.Multiplicate);

        public SimpleElement DefenseRate { get; } = new(1.0f, AggregateType.Multiplicate);

        public SimpleElement Defense { get; } = new(1.0f, AggregateType.Multiplicate);

        public SimpleElement Health { get; } = new(1.0f, AggregateType.Multiplicate);

        public void Set(float damage, float attackRate, float defenseRate, float defense, float health)
        {
            this.Damage.Value = ToFactor(damage);
            this.AttackRate.Value = ToFactor(attackRate);
            this.DefenseRate.Value = ToFactor(defenseRate);
            this.Defense.Value = ToFactor(defense);
            this.Health.Value = ToFactor(health);
        }

        private static float ToFactor(float percentage) => percentage > 0 ? 1.0f + (percentage / 100.0f) : 1.0f;
    }
}
