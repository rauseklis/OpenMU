// <copyright file="CombatEventKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

/// <summary>
/// The kinds of <see cref="CombatEvent"/>.
/// </summary>
public enum CombatEventKind
{
    /// <summary>A hit was applied to the health of an object.</summary>
    HitApplied,

    /// <summary>The same hit was delivered again and was ignored.</summary>
    HitDuplicateIgnored,

    /// <summary>A hit from a controller which was already replaced (for example an offline session which ended) was refused.</summary>
    HitRejectedStaleController,

    /// <summary>The life of an object ended. Raised exactly once per life.</summary>
    LifeEnded,

    /// <summary>Experience was added to a character in memory. Not yet saved.</summary>
    ExperienceSettled,

    /// <summary>The save which contains an earlier <see cref="ExperienceSettled"/> succeeded.</summary>
    ExperienceCommitted,
}
