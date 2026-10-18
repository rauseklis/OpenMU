// <copyright file="AuctionInventoryState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// The committed state of one item in a character's inventory storage.
/// </summary>
/// <param name="Id">The item identifier.</param>
/// <param name="Slot">The item slot.</param>
/// <param name="Durability">The durability (the stack size of stackable items).</param>
/// <param name="Level">The item level.</param>
public sealed record AuctionInventoryItemState(Guid Id, byte Slot, double Durability, byte Level);

/// <summary>
/// The committed state of a character's inventory storage: the items and the Zen. The Auction House changes both with
/// its own transactions, so a live player compares itself with this state afterwards instead of reconnecting.
/// </summary>
/// <param name="Money">The Zen of the inventory.</param>
/// <param name="Items">The items of the inventory storage, equipped ones included.</param>
public sealed record AuctionInventoryState(long Money, IReadOnlyList<AuctionInventoryItemState> Items);
