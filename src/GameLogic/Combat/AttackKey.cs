// <copyright file="AttackKey.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

/// <summary>
/// The server-owned identity of one damaging pulse of an attack: the attack action which was started
/// (<see cref="ActionId"/>, see <see cref="CombatIds.NextActionId"/>) and the number of the pulse inside of it.
/// Delivering the same key to the same target life a second time has no further effect.
/// </summary>
/// <param name="ActionId">The identifier of the attack action.</param>
/// <param name="Pulse">The number of the pulse inside of the action, starting at zero.</param>
public readonly record struct AttackKey(long ActionId, int Pulse);
