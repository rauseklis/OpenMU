// <copyright file="RequestModeChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments used by <see cref="RequestModeChatCommandPlugIn"/>.
/// </summary>
public class RequestModeChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the requested mode: on, off or auto.
    /// </summary>
    [Argument("mode", false)]
    public string? Mode { get; set; }
}
