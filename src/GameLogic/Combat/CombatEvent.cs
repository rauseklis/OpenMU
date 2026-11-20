// <copyright file="CombatEvent.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

/// <summary>
/// One fact about the resolution of an attack, reported after the game state has already been decided.
/// Consumers can observe it; they can neither change nor veto the outcome.
/// </summary>
/// <param name="Kind">The kind of the event.</param>
/// <param name="UtcTime">The time of the event.</param>
/// <param name="Key">The identity of the attack pulse (default for events which have none).</param>
/// <param name="LifeId">The life of the target.</param>
/// <param name="TargetObjectId">The object id of the target on its map.</param>
/// <param name="AttackerObjectId">The object id of the attacker on its map.</param>
/// <param name="AttackerName">The name of the attacker.</param>
/// <param name="CharacterId">The character which is concerned, if any (the attacker, or the one who earned experience).</param>
/// <param name="Controller">What steered the attacker.</param>
/// <param name="HealthBefore">The health of the target before the hit.</param>
/// <param name="HealthAfter">The health of the target after the hit.</param>
/// <param name="HealthDamage">The health damage which was reported by the damage calculation.</param>
/// <param name="ShieldDamage">The shield damage which was reported by the damage calculation.</param>
/// <param name="Experience">The experience which was earned (experience events only).</param>
public sealed record CombatEvent(
    CombatEventKind Kind,
    DateTime UtcTime,
    AttackKey Key,
    long LifeId,
    ushort TargetObjectId,
    ushort AttackerObjectId,
    string AttackerName,
    Guid? CharacterId,
    CombatController Controller,
    int HealthBefore,
    int HealthAfter,
    uint HealthDamage,
    uint ShieldDamage,
    int Experience);
