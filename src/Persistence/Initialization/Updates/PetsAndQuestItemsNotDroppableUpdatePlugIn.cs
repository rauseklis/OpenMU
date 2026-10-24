// <copyright file="PetsAndQuestItemsNotDroppableUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Makes the pets and mounts, the quest items and the account items (Magic Backpack, Vault Expansion Certificate,
/// Rage Fighter Character Card) undroppable, by applying the current <see cref="ItemRules"/> to the existing items.
/// </summary>
[PlugIn]
[Guid("3D9A6C52-8E14-4B7F-A5C0-72E1B94F6D08")]
public sealed class PetsAndQuestItemsNotDroppableUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Pets and quest items can't be dropped";

    /// <inheritdoc />
    public override string Description => "Pets, mounts, quest items and account items (Magic Backpack, Vault Expansion Certificate, Rage Fighter Character Card) can't be dropped on the ground anymore, matching the client item data.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.PetsAndQuestItemsNotDroppable;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        ItemRules.Apply(gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
