// <copyright file="SessionEventKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

/// <summary>
/// The kinds of <see cref="SessionEvent"/>.
/// </summary>
public enum SessionEventKind
{
    /// <summary>An account logged in.</summary>
    LoggedIn,

    /// <summary>A character entered the world.</summary>
    EnteredWorld,

    /// <summary>A session ended (the connection closed).</summary>
    Disconnected,

    /// <summary>The server took over a character for offline play.</summary>
    OfflineStarted,

    /// <summary>The offline play of a character ended.</summary>
    OfflineStopped,
}
