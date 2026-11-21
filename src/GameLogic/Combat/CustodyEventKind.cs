// <copyright file="CustodyEventKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

/// <summary>
/// The kinds of <see cref="CustodyEvent"/>.
/// </summary>
public enum CustodyEventKind
{
    /// <summary>An item was given to another player by a finished trade.</summary>
    ItemTraded,

    /// <summary>An item was sold by a player to another player through a personal store.</summary>
    ItemSoldToPlayer,

    /// <summary>A player bought an item from a merchant.</summary>
    ItemBoughtFromMerchant,

    /// <summary>A player sold an item to a merchant.</summary>
    ItemSoldToMerchant,

    /// <summary>A monster issued an item which a killer may pick up.</summary>
    ItemIssuedByMonster,
}
