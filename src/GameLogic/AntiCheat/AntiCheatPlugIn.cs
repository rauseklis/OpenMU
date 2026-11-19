// <copyright file="AntiCheatPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Holds the configuration of the generic protective measures (login throttle, connection limits, idle timeouts).
/// </summary>
[PlugIn]
[Display(Name = "Connection and login protection", Description = "Limits failed logins and connections per address and closes silent connections. Starts in shadow mode (log only).")]
[Guid("5B6E1D24-8C47-4F0A-9E73-2A1D6C8B4F95")]
public class AntiCheatPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<AntiCheatConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public AntiCheatConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new AntiCheatConfiguration();
}
