// <copyright file="ElfSoldierBuffDurationUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Skills;
using MUnique.OpenMU.PlugIns;

/// <summary>Sets the duration of the Elf Soldier buff to 2 hours 30 minutes.</summary>
[PlugIn]
[Guid("CF5AEABC-B63E-4A02-87CB-93FBCBFD109B")]
public sealed class ElfSoldierBuffDurationUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The duration in seconds (2 hours 30 minutes).</summary>
    internal const float DurationSeconds = 9000f;

    /// <inheritdoc />
    public override string Name => "Elf Soldier buff duration 2h 30min";

    /// <inheritdoc />
    public override string Description => "Sets the duration of the Elf Soldier buff to 2 hours 30 minutes.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ElfSoldierBuffDuration;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 18, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var effect = gameConfiguration.MagicEffects.FirstOrDefault(e => e.Number == (short)MagicEffectNumber.ElfSoldierBuff);
        if (effect?.Duration is { } duration)
        {
            duration.ConstantValue.Value = DurationSeconds;
        }

        return ValueTask.CompletedTask;
    }
}
