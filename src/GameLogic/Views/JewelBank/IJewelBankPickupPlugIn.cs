// <copyright file="IJewelBankPickupPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.JewelBank;

/// <summary>
/// Interface of a view whose implementation informs the player that a picked-up item went straight into the Jewel Bank.
/// </summary>
public interface IJewelBankPickupPlugIn : IViewPlugIn
{
    /// <summary>
    /// Notifies the client about an auto-deposited pickup.
    /// </summary>
    /// <param name="kindCode">The Jewel Bank kind code of the item.</param>
    /// <param name="units">The units which were credited.</param>
    /// <param name="newCount">The count of the kind after the credit.</param>
    /// <returns>A task which completes when the client is informed.</returns>
    ValueTask JewelBankPickupAsync(byte kindCode, uint units, ulong newCount);
}
