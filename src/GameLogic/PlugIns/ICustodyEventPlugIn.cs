// <copyright file="ICustodyEventPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin interface which receives the facts about items which changed hands or were issued.
/// The game state is decided before a plugin is called; a plugin which fails or is missing changes nothing.
/// </summary>
[Guid("A41F6C93-5D28-4E07-9B3A-72C0E8D154B6")]
[PlugInPoint("Item custody events", "Plugins which will be informed about traded, sold, bought and issued items.")]
public interface ICustodyEventPlugIn
{
    /// <summary>
    /// Called when an item changed hands or was issued.
    /// </summary>
    /// <param name="custodyEvent">The event.</param>
    void OnCustodyEvent(CustodyEvent custodyEvent);
}
