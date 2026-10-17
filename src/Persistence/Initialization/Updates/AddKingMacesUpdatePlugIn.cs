// <copyright file="AddKingMacesUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds king mace definitions without enabling drops or altering player items.
/// </summary>
[PlugIn]
[Guid("32EFDA91-3E48-45C7-BCC2-A6B8E5846FE1")]
public sealed class AddKingMacesUpdatePlugIn : UpdatePlugInBase
{
    public override string Name => "Add staged king mace weapons";
    public override string Description => "Adds Mace of King and Royal Mace of King with isolated +4 upgrade scaling and disabled drops.";
    public override UpdateVersion Version => UpdateVersion.AddKingMaces;
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
    public override bool IsMandatory => true;
    public override DateTime CreatedAt => new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new KingMaces(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
