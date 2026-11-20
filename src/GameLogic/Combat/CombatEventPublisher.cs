// <copyright file="CombatEventPublisher.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Hands <see cref="CombatEvent"/>s to the registered consumers. It is called after the game state was decided,
/// ignores the answer of the consumers and turns their failures into <see cref="CombatEventHealth"/> entries.
/// </summary>
internal static class CombatEventPublisher
{
    /// <summary>
    /// Publishes the event. Never throws.
    /// </summary>
    /// <param name="plugInManager">The plugin manager.</param>
    /// <param name="combatEvent">The event.</param>
    public static void Publish(PlugInManager? plugInManager, CombatEvent combatEvent)
    {
        try
        {
            plugInManager?.GetPlugInPoint<ICombatEventPlugIn>()?.OnCombatEvent(combatEvent);
        }
        catch (Exception ex)
        {
            CombatEventHealth.RecordFailure(ex);
        }
    }

    /// <summary>
    /// Determines what steers the attacker right now.
    /// </summary>
    /// <param name="attacker">The attacker.</param>
    /// <returns>The controller.</returns>
    public static CombatController ControllerOf(IAttacker attacker)
    {
        var player = attacker as Player ?? (attacker as IPlayerSurrogate)?.Owner;
        return player switch
        {
            null => CombatController.Other,
            OfflinePlayer => CombatController.OfflineServerOwned,
            _ when player.Attributes?[Stats.IsMuHelperActive] > 0 => CombatController.Helper,
            _ => CombatController.Manual,
        };
    }

    /// <summary>
    /// Gets the id of the character behind the attacker, if there is one.
    /// </summary>
    /// <param name="attacker">The attacker.</param>
    /// <returns>The id of the character.</returns>
    public static Guid? CharacterIdOf(IAttacker attacker)
    {
        var player = attacker as Player ?? (attacker as IPlayerSurrogate)?.Owner;
        return (player?.SelectedCharacter as Persistence.IIdentifiable)?.Id;
    }
}
