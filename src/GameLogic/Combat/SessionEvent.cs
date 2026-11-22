// <copyright file="SessionEvent.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

/// <summary>
/// One fact about a session of an account (login, entering the world, disconnecting, offline play), reported after it
/// happened. It ties the other events to the authenticated session they happened in.
/// </summary>
/// <param name="Kind">The kind of the event.</param>
/// <param name="UtcTime">The time of the event.</param>
/// <param name="AccountId">The id of the account (a stable internal id, not the login name).</param>
/// <param name="CharacterId">The id of the character, if one is selected.</param>
/// <param name="CharacterName">The name of the character, if one is selected.</param>
/// <param name="Address">The network address of the client, if known.</param>
/// <param name="Build">The build identifier (serial) the client sent at login, if known.</param>
/// <param name="Controller">What steers the character.</param>
/// <param name="Details">Additional text for some kinds of events (for example the fingerprint of an integrity report).</param>
public sealed record SessionEvent(
    SessionEventKind Kind,
    DateTime UtcTime,
    Guid? AccountId,
    Guid? CharacterId,
    string CharacterName,
    string Address,
    string Build,
    CombatController Controller,
    string Details = "");
