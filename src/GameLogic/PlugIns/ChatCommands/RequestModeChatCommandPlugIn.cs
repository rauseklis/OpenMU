// <copyright file="RequestModeChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which sets how the player reacts to requests of other players:
/// "/re on" shows them, "/re off" refuses them without showing them, "/re auto" accepts party requests automatically.
/// </summary>
[Guid("156E27CA-1339-4C87-A877-CCA45F250C9F")]
[PlugIn]
[Display(Name = nameof(PlugInResources.RequestModeChatCommandPlugIn_Name), Description = nameof(PlugInResources.RequestModeChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(RequestModeChatCommandArgs), CharacterStatus.Normal)]
public class RequestModeChatCommandPlugIn : ChatCommandPlugInBase<RequestModeChatCommandArgs>
{
    private const string Command = "/re";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc/>
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, RequestModeChatCommandArgs arguments)
    {
        if (player.GameContext.RequestFilterDisabled)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.RequestFilterDisabledOnServer)).ConfigureAwait(false);
            return;
        }

        if (!TryParseMode(arguments.Mode, out var mode))
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.RequestModeUsage), player.RequestMode.ToString().ToUpperInvariant()).ConfigureAwait(false);
            return;
        }

        player.RequestMode = mode;
        var message = mode switch
        {
            RequestMode.Off => nameof(PlayerMessage.RequestModeOff),
            RequestMode.Auto => nameof(PlayerMessage.RequestModeAuto),
            _ => nameof(PlayerMessage.RequestModeOn),
        };

        await player.ShowLocalizedBlueMessageAsync(message).ConfigureAwait(false);
    }

    private static bool TryParseMode(string? text, out RequestMode mode)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case "on":
                mode = RequestMode.On;
                return true;
            case "off":
                mode = RequestMode.Off;
                return true;
            case "auto":
                mode = RequestMode.Auto;
                return true;
            default:
                mode = default;
                return false;
        }
    }
}
