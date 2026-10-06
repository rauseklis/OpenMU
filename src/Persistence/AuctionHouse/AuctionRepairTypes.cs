// <copyright file="AuctionRepairTypes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// The outcome of a repair batch.
/// </summary>
/// <param name="Repaired">Ended listings which received their missing return collection.</param>
/// <param name="Skipped">Ended listings which were left for manual review, because their item is not outside every inventory.</param>
public readonly record struct AuctionRepairResult(int Repaired, int Skipped);
