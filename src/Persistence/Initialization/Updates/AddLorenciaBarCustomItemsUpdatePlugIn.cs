// <copyright file="AddLorenciaBarCustomItemsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>Adds the Invisible set and the Mace of King to the shop of Lumen the Barmaid in the Lorencia bar.</summary>
[PlugIn]
[Guid("D4A81B67-39E2-4C50-A7F3-6B1E0C9D5824")]
public sealed class AddLorenciaBarCustomItemsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Lorencia bar: Invisible set and Mace of King";

    /// <inheritdoc />
    public override string Description => "Lumen the Barmaid sells the Invisible Helm, Armor, Pants, Gloves and Boots and the Mace of King at level +0.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.LorenciaBarCustomItems;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 24, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new LorenciaBarCustomItems(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
