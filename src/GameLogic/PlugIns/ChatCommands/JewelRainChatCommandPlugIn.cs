// <copyright file="JewelRainChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.JewelRain;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command which lets a game master start the Jewel Rain right away, without countdown.
/// </summary>
[PlugIn]
[Display(Name = "Jewel Rain Chat Command", Description = "Handles the GM command /jewelrain, which starts the Jewel Rain event immediately.")]
[Guid("C2A9D5E7-31B4-4F86-A0D3-5E7B9C1F2A48")]
public class JewelRainChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/jewelrain";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        JewelRainPlugIn.RequestStart(player.GameContext);
        await player.ShowBlueMessageAsync("Jewel Rain is starting now.").ConfigureAwait(false);
    }
}
