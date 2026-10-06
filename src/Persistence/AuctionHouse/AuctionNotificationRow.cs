// <copyright file="AuctionNotificationRow.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// A notification which was claimed for delivery.
/// </summary>
/// <param name="Id">The notification identifier.</param>
/// <param name="AccountId">The account which is notified.</param>
/// <param name="ListingId">The listing, or <c>null</c>.</param>
/// <param name="Kind">The notification kind: 0 outbid, 1 sold, 2 bought, 3 expired, 4 returned.</param>
/// <param name="Currency">The currency mode as its wire value, or <c>null</c>.</param>
/// <param name="Amount">The amount, or <c>null</c>.</param>
/// <param name="CreatedAt">When the notification was created, in UTC.</param>
public readonly record struct AuctionNotificationRow(Guid Id, Guid AccountId, Guid? ListingId, byte Kind, byte? Currency, long? Amount, DateTimeOffset CreatedAt);
