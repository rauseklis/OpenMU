// <copyright file="CustodyEventPublisher.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Hands <see cref="CustodyEvent"/>s to the registered consumers. Never throws; failures are counted in <see cref="CombatEventHealth"/>.
/// </summary>
internal static class CustodyEventPublisher
{
    /// <summary>
    /// Publishes the event.
    /// </summary>
    /// <param name="plugInManager">The plugin manager.</param>
    /// <param name="custodyEvent">The event.</param>
    public static void Publish(PlugInManager? plugInManager, CustodyEvent custodyEvent)
    {
        try
        {
            plugInManager?.GetPlugInPoint<ICustodyEventPlugIn>()?.OnCustodyEvent(custodyEvent);
        }
        catch (Exception ex)
        {
            CombatEventHealth.RecordFailure(ex);
        }
    }
}
