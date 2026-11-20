// <copyright file="ICombatEventPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin interface which receives the facts about resolved attacks, deaths and experience settlements.
/// The game state is decided before a plugin is called. A plugin which fails or is missing does not
/// change what happens in the game; failures are counted in <see cref="CombatEventHealth"/>.
/// </summary>
[Guid("7E3B91A4-52C8-4D06-B1F7-9A64D2E8C035")]
[PlugInPoint("Combat events", "Plugins which will be informed about resolved hits, ended lives and experience settlements.")]
public interface ICombatEventPlugIn
{
    /// <summary>
    /// Called when something happened which a consumer may want to record.
    /// </summary>
    /// <param name="combatEvent">The event.</param>
    void OnCombatEvent(CombatEvent combatEvent);
}
