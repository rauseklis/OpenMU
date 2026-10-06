// <copyright file="AuctionReconciliationTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// One broken invariant found by the read-only reconciliation.
/// </summary>
/// <param name="Invariant">The name of the invariant which is broken.</param>
/// <param name="Subject">The identifier of the listing, item, or currency which breaks it.</param>
public readonly record struct AuctionReconciliationFinding(string Invariant, string Subject);
