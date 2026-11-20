// <copyright file="CombatIds.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

using System.Threading;

/// <summary>
/// Creates the process-wide unique identifiers of attack actions and of the lives of attackable objects.
/// The identifiers are owned by the server and never come from a client.
/// </summary>
public static class CombatIds
{
    private static long actionCounter;
    private static long lifeCounter;

    /// <summary>
    /// Gets a new identifier for an attack action (one swing, one skill cast, one offline attack round).
    /// </summary>
    /// <returns>The identifier.</returns>
    public static long NextActionId() => Interlocked.Increment(ref actionCounter);

    /// <summary>
    /// Gets a new identifier for the life of an attackable object. Every spawn starts a new life.
    /// </summary>
    /// <returns>The identifier.</returns>
    public static long NextLifeId() => Interlocked.Increment(ref lifeCounter);
}
