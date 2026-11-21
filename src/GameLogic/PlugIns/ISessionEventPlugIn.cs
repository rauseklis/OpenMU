// <copyright file="ISessionEventPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin interface which receives the facts about sessions: logins, entering the world, disconnects and offline play.
/// The game state is decided before a plugin is called; a plugin which fails or is missing changes nothing.
/// </summary>
[Guid("C7A25E48-0B63-4D91-8E1F-35B9D0A64C72")]
[PlugInPoint("Session events", "Plugins which will be informed about logins, entering the world, disconnects and offline play.")]
public interface ISessionEventPlugIn
{
    /// <summary>
    /// Called when something happened in a session.
    /// </summary>
    /// <param name="sessionEvent">The event.</param>
    void OnSessionEvent(SessionEvent sessionEvent);
}
