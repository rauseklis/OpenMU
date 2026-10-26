// <copyright file="SocketOptionPolicy.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Prevents old socket links from affecting items whose socket capability is disabled.
/// </summary>
public static class SocketOptionPolicy
{
    /// <summary>
    /// Determines whether an option belongs to the socket system.
    /// </summary>
    /// <param name="type">The option type.</param>
    /// <returns>Whether the option is a socket or socket bonus.</returns>
    public static bool IsSocketOption(ItemOptionType? type) => type == ItemOptionTypes.SocketOption || type == ItemOptionTypes.SocketBonusOption;

    /// <summary>
    /// Determines whether an option is active for an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="type">The option type.</param>
    /// <returns>Whether the option may affect this item.</returns>
    public static bool IsActive(Item item, ItemOptionType? type) => !IsSocketOption(type) || item.Definition?.MaximumSockets > 0;
}
