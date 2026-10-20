// <copyright file="JewelBankOptions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.JewelBank;

/// <summary>
/// The settings of the Jewel Bank.
/// </summary>
public static class JewelBankOptions
{
    /// <summary>
    /// The default for <see cref="MaximumPerKind"/>.
    /// </summary>
    public const long DefaultMaximumPerKind = 1_000_000;

    /// <summary>
    /// Gets or sets the most units the bank holds of one kind of jewel or fruit. A deposit or pickup which would exceed it
    /// is refused, so the item stays where it is.
    /// </summary>
    public static long MaximumPerKind { get; set; } = DefaultMaximumPerKind;

    /// <summary>
    /// Gets the shortest time between two mutating requests of one account.
    /// </summary>
    public static TimeSpan MinimumRequestInterval { get; } = TimeSpan.FromMilliseconds(250);
}
