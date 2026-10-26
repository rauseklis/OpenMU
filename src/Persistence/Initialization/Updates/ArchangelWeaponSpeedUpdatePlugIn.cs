// <copyright file="ArchangelWeaponSpeedUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sets the attack speed of the Divine Archangel weapons and the Absolute Scepter to <see cref="Speed"/>.
/// </summary>
[PlugIn]
[Guid("FDB8A013-FA3F-4C03-A618-93EF3F73CEF7")]
public sealed class ArchangelWeaponSpeedUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The attack speed of the weapons.
    /// </summary>
    public const int Speed = 50;

    /// <summary>
    /// The weapons (item group, item number) which get the attack speed.
    /// </summary>
    public static readonly IReadOnlyList<(byte Group, short Number)> Weapons =
    [
        (0, 19), // Divine Sword of Archangel
        (2, 13), // Divine Scepter of Archangel
        (2, 17), // Absolute Scepter
        (4, 18), // Divine Crossbow of Archangel
        (5, 10), // Divine Staff of Archangel
        (5, 36), // Divine Stick of Archangel
    ];

    /// <inheritdoc />
    public override string Name => "Attack speed 50 for Divine Archangel weapons";

    /// <inheritdoc />
    public override string Description => "Sets the attack speed of the Divine Sword, Scepter, Crossbow, Staff and Stick of Archangel and of the Absolute Scepter to 50, matching the client item data.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ArchangelWeaponSpeed;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (group, number) in Weapons)
        {
            var item = gameConfiguration.Items.SingleOrDefault(i => i.Group == group && i.Number == number);
            var powerUp = item?.BasePowerUpAttributes.SingleOrDefault(p => p.TargetAttribute?.Id == Stats.AttackSpeedByWeapon.Id);
            if (powerUp is not null)
            {
                powerUp.BaseValue = Speed;
            }
        }

        return ValueTask.CompletedTask;
    }
}
