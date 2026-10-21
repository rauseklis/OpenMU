// <copyright file="RenameRoyalMaceUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Renames the Royal Mace of King (2/23) to Royal Mace of Emberion in databases which installed it under the old name.
/// </summary>
[PlugIn]
[Guid("5B0C4E7A-3D1F-4A62-9E58-7C2D91B4A0F3")]
public sealed class RenameRoyalMaceUpdatePlugIn : UpdatePlugInBase
{
    private const string OldName = "Royal Mace of King";
    private const string NewName = "Royal Mace of Emberion";

    public override string Name => "Rename Royal Mace of King";
    public override string Description => "Renames the Royal Mace of King to Royal Mace of Emberion. Stats, drops and player items are unchanged.";
    public override UpdateVersion Version => UpdateVersion.RenameRoyalMace;
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
    public override bool IsMandatory => true;
    public override DateTime CreatedAt => new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var item = gameConfiguration.Items.SingleOrDefault(i => i.Group == 2 && i.Number == 23);
        if (item is not null && item.Name.ToString() == OldName)
        {
            item.Name = NewName;
        }

        return ValueTask.CompletedTask;
    }
}
