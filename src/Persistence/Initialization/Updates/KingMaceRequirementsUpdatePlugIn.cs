// <copyright file="KingMaceRequirementsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Updates existing king maces without changing player possessions or drop rates.
/// </summary>
[PlugIn]
[Guid("774E96A1-B2C5-4E82-BEDA-CF1500EA6421")]
public sealed class KingMaceRequirementsUpdatePlugIn : UpdatePlugInBase
{
    public override string Name => "Rare king mace requirements";
    public override string Description => "Sets rare mace Strength/Agility requirements and keeps both weapons skill-free.";
    public override UpdateVersion Version => UpdateVersion.KingMaceRequirements;
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
    public override bool IsMandatory => true;
    public override DateTime CreatedAt => new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);

    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new KingMaceRequirements(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
