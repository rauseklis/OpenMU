// <copyright file="AuctionNotificationDelivery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// What happened to one claimed notification.
/// </summary>
public enum AuctionNotificationDeliveryOutcome
{
    /// <summary>The player was online and received the message.</summary>
    Delivered,

    /// <summary>The account was offline; the Mailbox remains the durable record.</summary>
    NotOnline,
}

/// <summary>
/// The totals of one delivery batch.
/// </summary>
/// <param name="Delivered">The notifications shown to an online player.</param>
/// <param name="NotOnline">The notifications whose account was offline.</param>
/// <param name="Failed">The notifications whose delivery threw; they do not stop the batch.</param>
public readonly record struct AuctionNotificationDeliveryResult(int Delivered, int NotOnline, int Failed);
