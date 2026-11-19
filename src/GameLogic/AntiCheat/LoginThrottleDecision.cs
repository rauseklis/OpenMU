// <copyright file="LoginThrottleDecision.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// The result of <see cref="LoginThrottle.Check"/>.
/// </summary>
public enum LoginThrottleDecision
{
    /// <summary>The login attempt may proceed.</summary>
    Allow,

    /// <summary>This account is locked for this address.</summary>
    BlockedPair,

    /// <summary>This address is locked.</summary>
    BlockedIp,
}
