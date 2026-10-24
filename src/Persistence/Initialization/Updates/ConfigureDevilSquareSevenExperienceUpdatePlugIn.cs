// <copyright file="ConfigureDevilSquareSevenExperienceUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Updates only DS seven completion EXP to the revised amounts.
/// </summary>
[PlugIn]
[Guid("B4E128AE-20A3-43B7-823C-0AC04D1C14CF")]
public sealed class ConfigureDevilSquareSevenExperienceUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Devil Square seven completion EXP";

    /// <inheritdoc />
    public override string Description => "Sets DS seven completion EXP to 60, 55, 50 and 45 million without changing other rewards.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.DevilSquareSevenExperience;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 23, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new DevilSquareExperienceRewards(context, gameConfiguration).InitializeLevel(7);
        return ValueTask.CompletedTask;
    }
}
