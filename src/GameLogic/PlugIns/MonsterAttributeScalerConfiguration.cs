// <copyright file="MonsterAttributeScalerConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Configuration for the <see cref="MonsterAttributeScaler"/>.
/// </summary>
public class MonsterAttributeScalerConfiguration
{
    private float _scaleAllPercentage;
    private float _damagePercentage = 25.0f;
    private float _attackRatePercentage = 25.0f;
    private float _defenseRatePercentage = 25.0f;
    private float _defensePercentage = 25.0f;
    private float _healthPercentage = 25.0f;

    /// <summary>
    /// Gets or sets the percentage applied to all stats at once.
    /// When set above 0, cascades to all individual fields.
    /// </summary>
    public float ScaleAllPercentage
    {
        get => this._scaleAllPercentage;
        set
        {
            this._scaleAllPercentage = value;
            if (value > 0)
            {
                this._damagePercentage = value;
                this._attackRatePercentage = value;
                this._defenseRatePercentage = value;
                this._defensePercentage = value;
                this._healthPercentage = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the percentage by which monster damage (MinPhysBaseDmg, MaxPhysBaseDmg) is increased.
    /// </summary>
    public float DamagePercentage
    {
        get => this.ScaleAllActive ? this._scaleAllPercentage : this._damagePercentage;
        set => this.SetIndividualField(ref this._damagePercentage, value);
    }

    /// <summary>
    /// Gets or sets the percentage by which the monster attack rate is increased.
    /// </summary>
    public float AttackRatePercentage
    {
        get => this.ScaleAllActive ? this._scaleAllPercentage : this._attackRatePercentage;
        set => this.SetIndividualField(ref this._attackRatePercentage, value);
    }

    /// <summary>
    /// Gets or sets the percentage by which monster defense rate is increased.
    /// </summary>
    public float DefenseRatePercentage
    {
        get => this.ScaleAllActive ? this._scaleAllPercentage : this._defenseRatePercentage;
        set => this.SetIndividualField(ref this._defenseRatePercentage, value);
    }

    /// <summary>
    /// Gets or sets the percentage by which monster defense is increased.
    /// </summary>
    public float DefensePercentage
    {
        get => this.ScaleAllActive ? this._scaleAllPercentage : this._defensePercentage;
        set => this.SetIndividualField(ref this._defensePercentage, value);
    }

    /// <summary>
    /// Gets or sets the percentage by which monster maximum health is increased.
    /// </summary>
    public float HealthPercentage
    {
        get => this.ScaleAllActive ? this._scaleAllPercentage : this._healthPercentage;
        set => this.SetIndividualField(ref this._healthPercentage, value);
    }

    /// <summary>
    /// Gets or sets the internal id of the only game server on which the scaling is applied.
    /// If empty (default), it is applied on every game server. A change needs a server restart.
    /// </summary>
    public byte? OnlyServerId { get; set; }

    /// <summary>
    /// Gets or sets the internal id of one more game server which gets its own attack rate and health scaling
    /// (<see cref="OtherServerAttackRatePercentage"/>, <see cref="OtherServerHealthPercentage"/>). Damage, defense and defense rate
    /// are scaled by the same percentages as on the other server. If empty (default), there is no such server.
    /// A change needs a server restart.
    /// </summary>
    public byte? OtherServerId { get; set; }

    /// <summary>
    /// Gets or sets the percentage by which the monster attack rate is increased on the server <see cref="OtherServerId"/>.
    /// </summary>
    public float OtherServerAttackRatePercentage { get; set; }

    /// <summary>
    /// Gets or sets the percentage by which the monster maximum health is increased on the server <see cref="OtherServerId"/>.
    /// </summary>
    public float OtherServerHealthPercentage { get; set; }

    /// <summary>
    /// Gets or sets the monster numbers (comma separated, e.g. "43,53,54") which get their own boosted percentages
    /// (<see cref="BoostedDamagePercentage"/> and the following ones) instead of the normal ones. This is only used on the
    /// server <see cref="OnlyServerId"/> (or on every server, if that is empty). Empty (default) means no boosted monsters.
    /// A change needs a server restart.
    /// </summary>
    public string BoostedMonsterNumbers { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the percentage by which the damage of the boosted monsters is increased (100 = double).
    /// </summary>
    public float BoostedDamagePercentage { get; set; }

    /// <summary>
    /// Gets or sets the percentage by which the attack rate of the boosted monsters is increased (900 = ten times).
    /// </summary>
    public float BoostedAttackRatePercentage { get; set; }

    /// <summary>
    /// Gets or sets the percentage by which the defense rate of the boosted monsters is increased.
    /// </summary>
    public float BoostedDefenseRatePercentage { get; set; }

    /// <summary>
    /// Gets or sets the percentage by which the defense of the boosted monsters is increased.
    /// </summary>
    public float BoostedDefensePercentage { get; set; }

    /// <summary>
    /// Gets or sets the percentage by which the maximum health of the boosted monsters is increased.
    /// </summary>
    public float BoostedHealthPercentage { get; set; }

    private bool ScaleAllActive => this._scaleAllPercentage > 0;

    private void SetIndividualField(ref float field, float value)
    {
        if (this.ScaleAllActive)
        {
            return;
        }

        field = value;
    }
}
