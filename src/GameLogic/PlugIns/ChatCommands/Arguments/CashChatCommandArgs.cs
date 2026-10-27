// <copyright file="CashChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments used by CashChatCommandPlugIn.
/// </summary>
public class CashChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the character name of the player who receives the currency.
    /// </summary>
    [Argument("char")]
    public string? CharacterName { get; set; }

    /// <summary>
    /// Gets or sets the amount of WCoinC.
    /// </summary>
    [Argument("wcc", false)]
    public long WCoinC { get; set; }

    /// <summary>
    /// Gets or sets the amount of WCoinP.
    /// </summary>
    [Argument("wcp", false)]
    public long WCoinP { get; set; }

    /// <summary>
    /// Gets or sets the amount of Goblin Points.
    /// </summary>
    [Argument("gp", false)]
    public long GoblinPoints { get; set; }
}
