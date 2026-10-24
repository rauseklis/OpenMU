// <copyright file="ResetCharacterNpcPlugin.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Reset;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Opens the window of the reset NPC, in which the player sees the requirements of the next reset and does it.
/// </summary>
[Guid("08953BE6-DABF-49CC-A500-FDB9DC2C4D80")]
[PlugIn]
[Display(Name = nameof(PlugInResources.ResetCharacterNpcPlugin_Name), Description = nameof(PlugInResources.ResetCharacterNpcPlugin_Description), ResourceType = typeof(PlugInResources))]
public class ResetCharacterNpcPlugin : IPlayerTalkToNpcPlugIn
{
    /// <summary>
    /// Gets the reset NPC number of 'Raymond the Reset Master'.
    /// </summary>
    public static short ResetNpcNumber => 371;

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (npc.Definition.Number != ResetNpcNumber)
        {
            return;
        }

        eventArgs.HasBeenHandled = true;

        // The window of the NPC stays open until the player closes it: the server keeps the NPC as the opened one, so that
        // the requests of the window (information, reset) are accepted.
        eventArgs.LeavesDialogOpen = true;

        // The player decides in the window of the NPC: it shows the requirements and the current values, and the reset is
        // done when the player presses the button.
        var status = await new ResetCharacterAction(player, npc).GetStatusAsync().ConfigureAwait(false)
                     ?? new ResetNpcStatus(ResetNpcOutcome.Refused, 0, 0, 0, 0, 0, 0, 0, 0);
        await player.InvokeViewPlugInAsync<IShowResetNpcPlugIn>(p => p.ShowResetNpcAsync(status)).ConfigureAwait(false);
    }
}