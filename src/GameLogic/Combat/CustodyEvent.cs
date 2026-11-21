// <copyright file="CustodyEvent.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

/// <summary>
/// One fact about an item which changed hands or was issued, reported after it happened.
/// Consumers can observe it; they can neither change nor veto the outcome.
/// </summary>
/// <param name="Kind">The kind of the event.</param>
/// <param name="UtcTime">The time of the event.</param>
/// <param name="ItemId">The id of the item.</param>
/// <param name="ItemName">The name of the item definition.</param>
/// <param name="ItemLevel">The level of the item.</param>
/// <param name="FromCharacterId">The character which gave the item away (none for an item issued by a monster or a merchant).</param>
/// <param name="FromName">The name of the giver, of the merchant or of the monster.</param>
/// <param name="ToCharacterId">The character which received the item (none when a merchant received it).</param>
/// <param name="ToName">The name of the receiver, or of the merchant.</param>
/// <param name="Amount">The amount of zen for <see cref="CustodyEventKind.ZenTraded"/>; zero for the other kinds.</param>
public sealed record CustodyEvent(
    CustodyEventKind Kind,
    DateTime UtcTime,
    Guid? ItemId,
    string ItemName,
    byte ItemLevel,
    Guid? FromCharacterId,
    string FromName,
    Guid? ToCharacterId,
    string ToName,
    long Amount = 0);
