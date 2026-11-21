// <copyright file="ResendMagicEffectsAfterMapChangePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Resends all active visible magic effects to the client after a map change,
/// so that the buff icons reappear in the UI.
/// </summary>
[PlugIn]
[Guid("A1E3B5C7-9F42-4D18-8A6E-7C2F1D0E5B39")]
public sealed class ResendMagicEffectsAfterMapChangePlugIn : IPlayerStateChangedPlugIn
{
    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.ChangingMap || currentState != PlayerState.EnteredWorld)
        {
            return;
        }

        foreach (var effect in player.MagicEffectList.GetActiveEffectsSnapshot())
        {
            if (effect.IsDisposed || effect.IsDisposing || effect.Id >= 200)
            {
                continue;
            }

            await player.InvokeViewPlugInAsync<IActivateMagicEffectPlugIn>(p => p.ActivateMagicEffectAsync(effect, player)).ConfigureAwait(false);
        }
    }
}
