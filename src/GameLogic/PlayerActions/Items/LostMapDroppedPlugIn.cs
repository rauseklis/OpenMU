// <copyright file="LostMapDroppedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Items;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Creates the Kalima gate when a normal Lost Map is dropped.
/// </summary>
[PlugIn]
[Display(Name = nameof(LostMapDroppedPlugIn), Description = "This plugin handles the drop of the lost map item. It creates the gate to the kalima map.")]
[Guid("F6DB10E0-AE7F-4BC6-914F-B858763C5CF7")]
public sealed class LostMapDroppedPlugIn : IItemDropPlugIn
{
    /// <inheritdoc />
    public async ValueTask HandleItemDropAsync(Player player, Item item, Point target, IItemDropPlugIn.ItemDropArguments cancelArgs)
    {
        if (!item.IsLostMap())
        {
            return;
        }

        cancelArgs.WasHandled = true;
        cancelArgs.Success = await KalimaGateCreator.CreateAsync(player, item.Level, target).ConfigureAwait(false);
    }
}
