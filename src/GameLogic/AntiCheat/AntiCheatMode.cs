// <copyright file="AntiCheatMode.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// How a protective measure behaves.
/// </summary>
public enum AntiCheatMode
{
    /// <summary>
    /// The measure does nothing.
    /// </summary>
    Off,

    /// <summary>
    /// The measure only logs what it would have done.
    /// </summary>
    Shadow,

    /// <summary>
    /// The measure is applied.
    /// </summary>
    Enforce,
}
