// <copyright file="CombatController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

/// <summary>
/// Describes what is currently steering an attacker.
/// </summary>
public enum CombatController
{
    /// <summary>A player who is online and acts manually.</summary>
    Manual,

    /// <summary>A player who is online and whose MU Helper is running.</summary>
    Helper,

    /// <summary>A character which is played by the server on its own (offline leveling).</summary>
    OfflineServerOwned,

    /// <summary>Anything else (monsters, pets, scripted actors).</summary>
    Other,
}
