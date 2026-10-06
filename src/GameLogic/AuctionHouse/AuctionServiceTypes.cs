// <copyright file="AuctionServiceTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// The outcome of a service request. Values match the wire result catalog, so the packet handler converts them directly.
/// </summary>
public enum AuctionResultCode
{
    /// <summary>The request succeeded.</summary>
    Success = 0,

    /// <summary>The feature is disabled.</summary>
    Disabled = 1,

    /// <summary>The request is malformed or out of range.</summary>
    InvalidRequest = 8,

    /// <summary>The listing does not exist.</summary>
    ListingNotFound = 18,
}

/// <summary>
/// The result of opening the window.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="PendingMailbox">The number of collections the account has not finished claiming.</param>
/// <param name="ServerTime">The server time, in UTC.</param>
/// <param name="PageSize">The configured number of listings per page.</param>
public readonly record struct AuctionOpenResult(AuctionResultCode Result, int PendingMailbox, DateTimeOffset ServerTime, int PageSize);

/// <summary>
/// The result of a browse request.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="Page">The listings on the page; empty when the request failed.</param>
/// <param name="ServerTime">The server time, in UTC.</param>
public readonly record struct AuctionBrowseResult(AuctionResultCode Result, AuctionListingPage Page, DateTimeOffset ServerTime);

/// <summary>
/// The result of a detail request.
/// </summary>
/// <param name="Result">The outcome.</param>
/// <param name="Listing">The listing; <c>null</c> when the request failed.</param>
/// <param name="ServerTime">The server time, in UTC.</param>
public readonly record struct AuctionDetailResult(AuctionResultCode Result, AuctionListingDetail? Listing, DateTimeOffset ServerTime);

