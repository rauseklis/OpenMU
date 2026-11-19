// <copyright file="ConnectionDecision.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// The result of <see cref="ConnectionTracker.TryAccept"/>.
/// </summary>
public enum ConnectionDecision
{
    /// <summary>The connection is fine.</summary>
    Allow,

    /// <summary>The address has too many open connections.</summary>
    TooManyForAddress,

    /// <summary>The address opened too many connections recently.</summary>
    TooManyRecentForAddress,

    /// <summary>The game server has too many connections which did not log in yet.</summary>
    TooManyPreLogin,
}
